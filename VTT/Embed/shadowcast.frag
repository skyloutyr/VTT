#version 330 core
in vec2 f_tex;

uniform sampler2D positions;
uniform samplerBuffer boxes;
uniform samplerBuffer bvh;
uniform samplerBuffer indices;
uniform bool bvhHasData;
uniform bool noCursor;
uniform vec2 cursor_position;
uniform float shadow_opacity;
uniform float light_threshold;
uniform float light_dimming;

struct LightSource
{
    vec2 position;
    float threshold;
    float dimming;
};

uniform LightSource lights[64];
uniform int num_lights;

layout (location = 0) out float color;

const float FLOAT_MAX = 1e+30;

struct Ray
{
	vec2 origin;
	vec2 direction;
	vec2 inverseDirection;
	float t;
};

struct BVHNode
{
    vec4 bounds;
    int leftFirst;
    int primitiveCount;
};

struct OBB
{
    vec2 start;
    vec2 end;
    float rotation;
    vec2 rotationExtent;
};

BVHNode getNode(int index)
{
    vec4 dataA = texelFetch(bvh, (index * 2));
    vec4 dataB = texelFetch(bvh, (index * 2) + 1);
    return BVHNode(
        dataA,
        floatBitsToInt(dataB.x),
        floatBitsToInt(dataB.y)
    );
}

OBB getPrimitive(int index)
{
    int i = int(floatBitsToUint(texelFetch(indices, index).r));
    vec4 a = texelFetch(boxes, (i * 2));
    vec4 b = texelFetch(boxes, (i * 2) + 1);
    return OBB(a.xy, a.zw, b.x, b.yz);
}

OBB getPrimitiveByIndexDirectly(int i)
{
    vec4 a = texelFetch(boxes, (i * 2));
    vec4 b = texelFetch(boxes, (i * 2) + 1);
    return OBB(a.xy, a.zw, b.x, b.yz);
}

vec2 invDir(in vec2 dir)
{
    float x = 1.0 / dir.x;
    float y = 1.0 / dir.y;
    if (isinf(x) || isnan(x))
    {
        x = FLOAT_MAX;
    }

    if (isinf(y) || isnan(y))
    {
        y = FLOAT_MAX;
    }

    return vec2(x, y);
}

vec2 rotate(vec2 v, float a) 
{
	float cs = cos(a);
    float sn = sin(a);
    return vec2(
        v.x * cs - v.y * sn,
        v.x * sn + v.y * cs
    );
}

bool aabbContains(in vec4 box, in vec2 pos)
{
    return pos.x >= box.x && pos.y >= box.y && pos.x <= box.z && pos.y <= box.w;
}

bool obbContains(in OBB box, in vec2 pos)
{
    vec2 c = box.start + ((box.end - box.start) * 0.5);
    vec2 arel = pos - c;
    vec2 np = rotate(arel, box.rotation + 3.1415) + c;
    return aabbContains(vec4(box.start, box.end), np);
}

float aabbIntersect(in vec4 box, in Ray r)
{
	float tx1 = (box.x - r.origin.x) * r.inverseDirection.x;
	float tx2 = (box.z - r.origin.x) * r.inverseDirection.x;
    float tmin = min(tx1, tx2);
    float tmax = max(tx1, tx2);
    float ty1 = (box.y - r.origin.y) * r.inverseDirection.y;
    float ty2 = (box.w - r.origin.y) * r.inverseDirection.y;
    tmin = max(tmin, min(ty1, ty2));
    tmax = min(tmax, max(ty1, ty2));
    return (tmax >= tmin && tmin < r.t && tmax > 0) ? tmin : FLOAT_MAX;
}

float obbIntersect(in OBB box, in Ray r)
{
    if (box.rotation == 0)
    {
        return aabbIntersect(vec4(box.start, box.end), r);
    }
    else
    {
        vec2 c = box.start + ((box.end - box.start) * 0.5);
        vec2 arel = r.origin - c;
        vec2 rdir = rotate(r.direction, box.rotation + 3.1415);
	    Ray r1 = Ray(
            rotate(arel, box.rotation + 3.1415) + c,
            rdir,
            invDir(rdir),
            r.t
        );

        return aabbIntersect(vec4(box.start, box.end), r1);
    }
}

bool raycast(in Ray r)
{
    int stackptr = 0;
	BVHNode node = getNode(0);
	BVHNode nodeStack[64];

	while (true)
    {
        if (node.primitiveCount > 0)
        {
            for (int i = 0; i < node.primitiveCount; ++i)
            {
                if (obbIntersect(getPrimitive(node.leftFirst + i), r) <= r.t)
                {
                    return true;
                }
            }

            if (stackptr == 0)
            {
                break;
            }
            else
            {
                node = nodeStack[--stackptr];
            }

            continue;
        }

        BVHNode child1 = getNode(node.leftFirst);
        BVHNode child2 = getNode(node.leftFirst + 1);
        float dist1 = aabbIntersect(child1.bounds, r);
        float dist2 = aabbIntersect(child2.bounds, r);
        if (dist1 > dist2)
        {
            float t = dist1;
            dist1 = dist2;
            dist2 = t;

            BVHNode te = child1;
            child1 = child2;
            child2 = te;
        }

        if (dist1 >= r.t)
        {
            if (stackptr == 0)
            {
                break;
            }
            else
            {
                node = nodeStack[--stackptr];
            }
        }
        else
        {
            node = child1;
            if (dist2 < r.t)
            {
                nodeStack[stackptr++] = child2;
            }
        }
    }

	return false;
}

float raycastLight(vec2 f_world_position, LightSource light)
{
    vec2 v = light.position - f_world_position;
    vec2 vn = normalize(v);
    float d = length(v);
    Ray r = Ray(
        f_world_position,
        vn,
        invDir(vn),
        d
    );

    bool result = raycast(r);
    if (result)
    {
        return 0;
    }
    else
    {
        return d > light.threshold ? 0 : d > light.dimming ? (1.0 - ((d - light.dimming) / (light.threshold - light.dimming))) : 1;
    }
}

void main()
{
    if (noCursor)
    {
        color = shadow_opacity;
    }
    else
    {
        vec4 vp = texture(positions, f_tex);
        if (bvhHasData && vp.w > 1e-5)
        {
	        color = shadow_opacity;
            vec2 f_world_position = vp.xy;
            float d = length(f_world_position - cursor_position);
            if (num_lights == 0)
            {
                if (d > light_threshold)
                {
                    color = shadow_opacity;
                }
                else
                {
                    Ray r = Ray(
                        f_world_position,
                        normalize(cursor_position - f_world_position),
                        vec2(1.0, 1.0) / normalize(cursor_position - f_world_position),
                        d
                    );

                    bool result = raycast(r);
                    if (result)
                    {
                        color = shadow_opacity;
                    }
                    else
                    {
                        float fact = d > light_dimming ? (1.0 - ((d - light_dimming) / (light_threshold - light_dimming))) : 1;
                        color = max(shadow_opacity, fact);
                    }
                }
            }
            else
            {
                Ray r = Ray(
                    f_world_position,
                    normalize(cursor_position - f_world_position),
                    vec2(1.0, 1.0) / normalize(cursor_position - f_world_position),
                    d
                );

                bool result = raycast(r);
                if (result)
                {
                    color = shadow_opacity;
                }
                else
                {
                    float fact = d > light_threshold ? 0 : d > light_dimming ? (1.0 - ((d - light_dimming) / (light_threshold - light_dimming))) : 1;
                    for (int i = 0; i < num_lights; ++i)
                    {
                        float r = raycastLight(f_world_position, lights[i]);
                        fact = max(fact, r);
                    }

                    color = max(shadow_opacity, fact);
                }
            }
        }
        else
        {
            color = 1.0;
        }
    }
}