namespace VTT.Util
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;

    /**
 *  Copyright (C) 2011 by Morten S. Mikkelsen
 *
 *  This software is provided 'as-is', without any express or implied
 *  warranty.  In no event will the authors be held liable for any damages
 *  arising from the use of this software.
 *
 *  Permission is granted to anyone to use this software for any purpose,
 *  including commercial applications, and to alter it and redistribute it
 *  freely, subject to the following restrictions:
 *
 *  1. The origin of this software must not be misrepresented; you must not
 *     claim that you wrote the original software. If you use this software
 *     in a product, an acknowledgment in the product documentation would be
 *     appreciated but is not required.
 *  2. Altered source versions must be plainly marked as such, and must not be
 *     misrepresented as being the original software.
 *  3. This notice may not be removed or altered from any source distribution.
 */
    public static class MikkTSpace
    {
        private struct Context
        {
            public uint[] indices;
            public List<Vector3> positions;
            public List<Vector2> uvs;
            public List<Vector3> normals;
        }

        public struct TangentData
        {
            public Vector3 tan;
            public Vector3 bitan;
            public float magS;
            public float magT;
            public bool orient;

            public TangentData(Vector3 tan, Vector3 bitan, float magS, float magT, bool orient)
            {
                this.tan = tan;
                this.bitan = bitan;
                this.magS = magS;
                this.magT = magT;
                this.orient = orient;
            }
        }

        public static bool GenTangentSpace(uint[] indices, List<Vector3> positions, List<Vector2> uvs, List<Vector3> normals, out TangentData[] result, float angularThreshold = 180.0f)
        {
            Context ctx = new Context() { indices = indices, positions = positions, uvs = uvs, normals = normals };
            int nrFaces = indices.Length / 3;
            int nrTrianglesIn = nrFaces;
            float fThresCos = MathF.Cos(angularThreshold * MathF.PI / 180.0f);

            if (nrTrianglesIn <= 0)
            {
                result = null;
                return false;
            }

            // allocate memory for an index list
            int[] triListIn = new int[nrTrianglesIn * 3];
            Triangle[] triInfos = new Triangle[nrTrianglesIn];
            for (int i1 = 0; i1 < triInfos.Length; i1++)
            {
                Triangle tri = new Triangle();
                tri.vertexNum = new int[4];
                tri.assignedGroups = new Group[3];
                tri.faceNeighbours = new int[3];
                triInfos[i1] = tri;
            }

            // make an initial triangle --> face index list
            int nrTSPaces = GenerateInitialVerticesIndexList(triInfos, triListIn, ctx, nrTrianglesIn);

            // make a welded index list of identical positions and attributes (pos, norm, texc)
            //printf("gen welded index list begin\n");
            GenerateSharedVerticesIndexList(triListIn, ctx, nrTrianglesIn);
            //printf("gen welded index list end\n");

            // Mark all degenerate triangles
            int totTris = nrTrianglesIn;
            int degenTriangles = 0;
            for (int t = 0; t < totTris; t++)
            {
                int i0 = triListIn[(t * 3) + 0];
                int i1 = triListIn[(t * 3) + 1];
                int i2 = triListIn[(t * 3) + 2];
                Vector3 p0 = ctx.positions[i0];
                Vector3 p1 = ctx.positions[i1];
                Vector3 p2 = ctx.positions[i2];
                if (Equals(p0, p1) || Equals(p0, p2) || Equals(p1, p2))  // degenerate
                {
                    triInfos[t].flag |= AlgorithmFlags.MarkDegenerate;
                    ++degenTriangles;
                }
            }

            nrTrianglesIn = totTris - degenTriangles;

            // mark all triangle pairs that belong to a quad with only one
            // good triangle. These need special treatment in DegenEpilogue().
            // Additionally, move all good triangles to the start of
            // pTriInfos[] and piTriListIn[] without changing order and
            // put the degenerate triangles last.
            DegenPrologue(triInfos, triListIn, nrTrianglesIn, totTris);


            // evaluate triangle level attributes and neighbor list
            //printf("gen neighbors list begin\n");
            InitTriInfo(triInfos, triListIn, ctx, nrTrianglesIn);
            //printf("gen neighbors list end\n");


            // based on the 4 rules, identify groups based on connectivity
            int nrMaxGroups = nrTrianglesIn * 3;
            Group[] groups = new Group[nrMaxGroups];
            for (int j = 0; j < nrMaxGroups; ++j)
            {
                groups[j] = new Group();
            }

            int[] groupTrianglesBuffer =  new int[nrTrianglesIn * 3];
            //printf("gen 4rule groups begin\n");
            int nrActiveGroups = Build4RuleGroups(triInfos, groups, groupTrianglesBuffer, triListIn, nrTrianglesIn);
            //printf("gen 4rule groups end\n");

            //

            TSpace[] tspace = new TSpace[nrTSPaces];

            for (int t = 0; t < nrTSPaces; t++)
            {
                tspace[t] = new TSpace()
                {
                    os = new Vector3(1, 0, 0),
                    ot = new Vector3(0, 1, 0),
                    magS = 1,
                    magT = 1
                };
            }

            // make tspaces, each group is split up into subgroups if necessary
            // based on fAngularThreshold. Finally a tangent space is made for
            // every resulting subgroup
            //printf("gen tspaces begin\n");
            GenerateTSpaces(tspace, triInfos, groups, nrActiveGroups, triListIn, fThresCos, ctx);
            //printf("gen tspaces end\n");

            // degenerate quads with one good triangle will be fixed by copying a space from
            // the good triangle to the coinciding vertex.
            // all other degenerate triangles will just copy a space from any good triangle
            // with the same welded index in piTriListIn[].
            DegenEpilogue(tspace, triInfos, triListIn, ctx, nrTrianglesIn, totTris);


            int index = 0;
            result = new TangentData[nrFaces * 3];
            for (int f = 0; f < nrFaces; f++)
            {

                // I've decided to let degenerate triangles and group-with-anythings
                // vary between left/right hand coordinate systems at the vertices.
                // All healthy triangles on the other hand are built to always be either or.

                /*// force the coordinate system orientation to be uniform for every face.
                // (this is already the case for good triangles but not for
                // degenerate ones and those with bGroupWithAnything==true)
                bool bOrient = psTspace[index].bOrient;
                if (psTspace[index].iCounter == 0)	// tspace was not derived from a group
                {
                    // look for a space created in GenerateTSpaces() by iCounter>0
                    bool bNotFound = true;
                    int i=1;
                    while (i<verts && bNotFound)
                    {
                        if (psTspace[index+i].iCounter > 0) bNotFound=false;
                        else ++i;
                    }
                    if (!bNotFound) bOrient = psTspace[index+i].bOrient;
                }*/

                // set data
                for (int i = 0; i < 3; i++)
                {
                    TSpace pTSpace = tspace[index];
                    Vector3 tan = pTSpace.os;
                    Vector3 bitan = pTSpace.ot;
                    result[(f * 3) + i] = new TangentData(tan, bitan, pTSpace.magS, pTSpace.magT, pTSpace.orient);
                    ++index;
                }
            }

            return true;
        }

        private static int FindGridCell(float fMin, float fMax, float fVal)
        {
            float fIndex = 2048 * ((fVal - fMin) / (fMax - fMin));
            int iIndex = (int)fIndex;
            return iIndex < 2048 ? (iIndex >= 0 ? iIndex : 0) : (2048 - 1);
        }

        private static bool NotZero(float f) => MathF.Abs(f) > 1e-5f;
        private static bool NotZero(Vector3 v) => NotZero(v.X) && NotZero(v.Y) && NotZero(v.Z);

        private static int MakeIndex(int face, int vert) => (face << 2) | (vert & 3);

        private static TSpace AvgTSpace(TSpace ts0, TSpace ts1)
        {
            TSpace ret = new TSpace();

            if (ts0.magS == ts1.magS && ts0.magT == ts1.magT &&
               Equals(ts0.os, ts1.os) && Equals(ts0.ot, ts1.ot))
            {
                ret.magS = ts0.magS;
                ret.magT = ts0.magT;
                ret.os = ts0.os;
                ret.ot = ts0.ot;
            }
            else
            {
                ret.magS = 0.5f * (ts0.magS + ts1.magS);
                ret.magT = 0.5f * (ts0.magT + ts1.magT);
                ret.os = Vector3.Add(ts0.os, ts1.os);
                ret.ot = Vector3.Add(ts0.ot, ts1.ot);
                if (NotZero(ret.os))
                {
                    ret.os = Vector3.Normalize(ret.os);
                }

                if (NotZero(ret.ot))
                {
                    ret.ot = Vector3.Normalize(ret.ot);
                }
            }

            return ret;
        }

        private static int GenerateInitialVerticesIndexList(Triangle[] triangles, int[] triangleIndicesList, Context ctx, int numTriangles)
        {
            int tSpacesOffs = 0;
            int dstTriIndex = 0;
	        for (int f = 0; f < ctx.indices.Length / 3; f++)
	        {
		        triangles[dstTriIndex].orgFaceNumber = f;
		        triangles[dstTriIndex].tSpacesOffset = tSpacesOffs;
			    int[] pVerts = triangles[dstTriIndex].vertexNum;
                pVerts[0] = 0; 
                pVerts[1] = 1; 
                pVerts[2] = 2;
                int x = f * 3;
                triangleIndicesList[(dstTriIndex * 3) + 0] = (int)ctx.indices[x + 0];
                triangleIndicesList[(dstTriIndex * 3) + 1] = (int)ctx.indices[x + 1];
                triangleIndicesList[(dstTriIndex * 3) + 2] = (int)ctx.indices[x + 2];
			    ++dstTriIndex;	// next
		        tSpacesOffs += 3;
	        }

            for (int t = 0; t < numTriangles; t++)
            {
                triangles[t].flag = 0;
            }

            return tSpacesOffs;
        }

        private static void GenerateSharedVerticesIndexList(int[] triangleIndicesList, Context ctx, int numTriangles)
        {
            Vector3 vMin = ctx.positions[0], vMax = vMin, vDim;
            float fMin, fMax;
            for (int i = 1; i < (numTriangles * 3); i++)
	        {
		        int index = triangleIndicesList[i];
                Vector3 vP = ctx.positions[index];
                if (vMin.X > vP.X)
                {
                    vMin.X = vP.X;
                }
                else if (vMax.X < vP.X)
                {
                    vMax.X = vP.X;
                }

                if (vMin.Y > vP.Y)
                {
                    vMin.Y = vP.Y;
                }
                else if (vMax.Y < vP.Y)
                {
                    vMax.Y = vP.Y;
                }

                if (vMin.Z > vP.Z)
                {
                    vMin.Z = vP.Z;
                }
                else if (vMax.Z < vP.Z)
                {
                    vMax.Z = vP.Z;
                }
            }

            vDim = Vector3.Subtract(vMax, vMin);
            int channel = 0;
            fMin = vMin.X; 
            fMax = vMax.X;
            if (vDim.Y > vDim.X && vDim.Y > vDim.Z)
	        {
                channel = 1;
		        fMin = vMin.Y;
		        fMax = vMax.Y;
	        }

            else if (vDim.Z > vDim.X)
            {
                channel = 2;
                fMin = vMin.Z;
                fMax = vMax.Z;
            }

            // make allocations
            int[] hashTable = new int[numTriangles * 3];
            int[] hashCount = new int[2048];
            int[] hashOffsets = new int[2048];
            int[] hashCount2 = new int[2048];

            // count amount of elements in each cell unit
            for (int i = 0; i < (numTriangles * 3); i++)
            {
                int index = triangleIndicesList[i];
                Vector3 vP = ctx.positions[index];
                float fVal = channel == 0 ? vP.X : (channel == 1 ? vP.Y : vP.Z);
                int cell = FindGridCell(fMin, fMax, fVal);
                ++hashCount[cell];
            }

            // evaluate start index of each cell.
            hashOffsets[0] = 0;
            for (int k = 1; k < 2048; k++)
            {
                hashOffsets[k] = hashOffsets[k - 1] + hashCount[k - 1];
            }

            // insert vertices
            for (int i = 0; i < (numTriangles * 3); i++)
            {
                int index = triangleIndicesList[i];
                Vector3 vP = ctx.positions[index];
                float fVal = channel == 0 ? vP.X : (channel == 1 ? vP.Y : vP.Z);
                int cell = FindGridCell(fMin, fMax, fVal);
                hashTable[hashOffsets[cell]] = i;
                ++hashCount2[cell];
            }

            // find maximum amount of entries in any hash entry
            int maxCount = hashCount[0];
            for (int k = 1; k < 2048; k++)
            {
                if (maxCount < hashCount[k])
                {
                    maxCount = hashCount[k];
                }
            }

            TempVert[] tempVert = new TempVert[maxCount];
            for (int j = 0; j < maxCount; ++j)
            {
                tempVert[j] = new TempVert();
            }

            // complete the merge
            for (int k = 0; k < 2048; k++)
            {
                // extract table of cell k and amount of entries in it
                ArrayPointerWrapper<int> table = new ArrayPointerWrapper<int>(hashTable, hashOffsets[k]);
                int numEntries = hashCount[k];
                if (numEntries < 2)
                {
                    continue;
                }

                if (tempVert != null)
                {
                    for (int e = 0; e < numEntries; e++)
                    {
                        int j = table[e];
                        Vector3 vP = ctx.positions[triangleIndicesList[j]];
                        tempVert[e].vert.X = vP.X; 
                        tempVert[e].vert.Y = vP.Y;
                        tempVert[e].vert.Z = vP.Z; 
                        tempVert[e].index = j;
                    }

                    MergeVertsFast(triangleIndicesList, tempVert, ctx, 0, numEntries - 1);
                }
                else
                {
                    MergeVertsSlow(triangleIndicesList, ctx, table, numEntries);
                }
            }
        }

        private static void MergeVertsFast(int[] triangleIndicesList, TempVert[] tempVertices, Context ctx, int left, int right)
        {
            float[] min = new float[3], max = new float[3];
            for (int c = 0; c < 3; c++)
            {
                Vector3 vert = tempVertices[left].vert;
                min[c] = c == 0 ? vert.X : c == 1 ? vert.Y : vert.Z; 
                max[c] = min[c]; 
            }

            for (int l = (left + 1); l <= right; l++)
            {
                for (int c = 0; c < 3; c++)
                {
                    Vector3 vert = tempVertices[l].vert;
                    float fv = c == 0 ? vert.X : c == 1 ? vert.Y : vert.Z;
                    if (min[c] > fv)
                    {
                        min[c] = fv;
                    }

                    if (max[c] < fv)
                    {
                        max[c] = fv;
                    }
                }
	        }

	        float dx = max[0] - min[0];
            float dy = max[1] - min[1];
            float dz = max[2] - min[2];

            int channel = 0;
            if (dy > dx && dy > dz)
            {
                channel = 1;
            }
            else if (dz > dx)
            {
                channel = 2;
            }

            float sep = 0.5f * (max[channel] + min[channel]);

            // stop if all vertices are NaNs
            if (float.IsNaN(sep))
            {
                return;
            }

            // terminate recursion when the separation/average value
            // is no longer strictly between fMin and fMax values.
            if (sep >= max[channel] || sep <= min[channel])
            {
                // complete the weld
                for (int l = left; l <= right; l++)
                {
                    int i = tempVertices[l].index;
                    int index = triangleIndicesList[i];
                    Vector3 p = ctx.positions[index];
                    Vector3 n = ctx.normals[index];
                    Vector2 t = ctx.uvs[index];

                    bool notFound = true;
                    int l2 = left, i2rec = -1;
                    while (l2 < l && notFound)
                    {
                        int i2 = tempVertices[l2].index;
                        int index2 = triangleIndicesList[i2];
                        Vector3 vP2 = ctx.positions[index2];
                        Vector3 vN2 = ctx.normals[index2];
                        Vector2 vT2 = ctx.uvs[index2];
                        i2rec = i2;

                        //if (vP==vP2 && vN==vN2 && vT==vT2)
                        if (p.X == vP2.X && p.Y == vP2.Y && p.Z == vP2.Z &&
                            n.X == vN2.X && n.Y == vN2.Y && n.Z == vN2.Z &&
                            t.X == vT2.X && t.Y == vT2.Y)
                        {
                            notFound = false;
                        }
                        else
                        {
                            ++l2;
                        }
                    }

                    // merge if previously found
                    if (!notFound)
                    {
                        triangleIndicesList[i] = triangleIndicesList[i2rec];
                    }
                }
            }
            else
            {
                int l = left, r = right;
                // separate (by fSep) all points between iL_in and iR_in in pTmpVert[]
                while (l < r)
                {
                    bool readyLeftSwap = false, readyRightSwap = false;
                    while ((!readyLeftSwap) && l < r)
                    {
                        Vector3 vert = tempVertices[l].vert;
                        readyLeftSwap = !((channel == 0 ? vert.X : channel == 1 ? vert.Y : vert.Z) < sep);
                        if (!readyLeftSwap)
                        {
                            ++l;
                        }
                    }
                    while ((!readyRightSwap) && l < r)
                    {
                        Vector3 vert = tempVertices[r].vert;
                        readyRightSwap = (channel == 0 ? vert.X : channel == 1 ? vert.Y : vert.Z) < sep;
                        if (!readyRightSwap)
                        {
                            --r;
                        }
                    }

                    if (readyLeftSwap && readyRightSwap)
                    {
                        (tempVertices[r], tempVertices[l]) = (tempVertices[l], tempVertices[r]);
                        ++l; --r;
                    }
                }

                if (l == r)
                {
                    Vector3 vert = tempVertices[r].vert;
                    bool readyRightSwap = (channel == 0 ? vert.X : channel == 1 ? vert.Y : vert.Z) < sep;
                    if (readyRightSwap)
                    {
                        ++l;
                    }
                    else
                    {
                        --r;
                    }
                }

                // only need to weld when there is more than 1 instance of the (x,y,z)
                if (left < r)
                {
                    MergeVertsFast(triangleIndicesList, tempVertices, ctx, left, r);    // weld all left of fSep
                }

                if (l < right)
                {
                    MergeVertsFast(triangleIndicesList, tempVertices, ctx, l, right);    // weld all right of (or equal to) fSep
                }
            }
        }

        private static void MergeVertsSlow(int[] triangleIndicesList, Context ctx, ArrayPointerWrapper<int> table, int numEntries)
        {
            // this can be optimized further using a tree structure or more hashing.
            for (int e = 0; e < numEntries; e++)
	        {
		        int i = table[e];
                int index = triangleIndicesList[i];
                Vector3 p = ctx.positions[index];
                Vector3 n = ctx.normals[index];
                Vector2 t = ctx.uvs[index];

                bool notFound = true;
                int e2 = 0, i2rec = -1;
                while (e2 < e && notFound)
		        {
			        int i2 = table[e2];
                    int index2 = triangleIndicesList[i2];
                    Vector3 vP2 = ctx.positions[index2];
                    Vector3 vN2 = ctx.normals[index2];
                    Vector2 vT2 = ctx.uvs[index2];
                    i2rec = i2;
                    if (Equals(p, vP2) && Equals(n, vN2) && Equals(t, vT2))
                    {
                        notFound = false;
                    }
                    else
                    {
                        ++e2;
                    }
                }

                if (!notFound)
                {
                    triangleIndicesList[i] = triangleIndicesList[i2rec];
                }
            }
        }

        private static void DegenPrologue(Triangle[] triangles, int[] triangleIndicesList, int numTriangles, int numTotalTriangles)
        {
            bool stillFindingGoodOnes;

            // locate quads with only one good triangle
            int t = 0;
            while (t < (numTotalTriangles - 1))
	        {
		        int faceA = triangles[t].orgFaceNumber;
                int faceB = triangles[t + 1].orgFaceNumber;
                if (faceA == faceB) // this is a quad
                {
                    bool isDegenerateA = (triangles[t].flag & AlgorithmFlags.MarkDegenerate) != 0;
                    bool isDegenerateB = (triangles[t + 1].flag & AlgorithmFlags.MarkDegenerate) != 0;
			        if (isDegenerateA != isDegenerateB)
			        {
                        triangles[t].flag |= AlgorithmFlags.QuadOneDegenTri;
				        triangles[t + 1].flag |= AlgorithmFlags.QuadOneDegenTri;
			        }

                    t += 2;
		        }

                else
                {
                    ++t;
                }
            }

	        // reorder list so all degen triangles are moved to the back
	        // without reordering the good triangles
	        int nextGoodTriangleSearchIndex = 1;
            t = 0;
            stillFindingGoodOnes = true;
            while (t < numTriangles && stillFindingGoodOnes)
            {
                bool isGood = (triangles[t].flag & AlgorithmFlags.MarkDegenerate) == 0;
                if (isGood)
                {
                    if (nextGoodTriangleSearchIndex < (t + 2))
                    {
                        nextGoodTriangleSearchIndex = t + 2;
                    }
                }
                else
                {
                    int t0, t1;
                    // search for the first good triangle.
                    bool justADegenerate = true;
                    while (justADegenerate && nextGoodTriangleSearchIndex < numTotalTriangles)
                    {
                        bool bIsGood1 = (triangles[nextGoodTriangleSearchIndex].flag & AlgorithmFlags.MarkDegenerate) == 0;
                        if (bIsGood1)
                        {
                            justADegenerate = false;
                        }
                        else
                        {
                            ++nextGoodTriangleSearchIndex;
                        }
                    }

                    t0 = t;
                    t1 = nextGoodTriangleSearchIndex;
                    ++nextGoodTriangleSearchIndex;

                    // swap triangle t0 and t1
                    if (!justADegenerate)
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            (triangleIndicesList[(t1 * 3) + i], triangleIndicesList[(t0 * 3) + i]) = (triangleIndicesList[(t0 * 3) + i], triangleIndicesList[(t1 * 3) + i]);
                        }

                        (triangles[t1], triangles[t0]) = (triangles[t0], triangles[t1]);
                    }
                    else
                    {
                        stillFindingGoodOnes = false; // this is not supposed to happen
                    }
                }

                if (stillFindingGoodOnes)
                {
                    ++t;
                }
            }
        }

        private static void DegenEpilogue(TSpace[] tSpaces, Triangle[] triangles, int[] triangleIndicesList, Context ctx, int numTriangles, int numTotalTriangles)
        {

            // deal with degenerate triangles
            // punishment for degenerate triangles is O(N^2)
            for (int t = numTriangles; t < numTotalTriangles; t++)
	        {
                // degenerate triangles on a quad with one good triangle are skipped
                // here but processed in the next loop
                bool skip = (triangles[t].flag & AlgorithmFlags.QuadOneDegenTri) != 0;
		        if (!skip)
		        {
                    for (int i = 0; i < 3; i++)
			        {
                        int index1 = triangleIndicesList[(t * 3) + i];
                        // search through the good triangles
                        bool notFound = true;
                        int j = 0;
                        while (notFound && j < (3 * numTriangles))
				        {

                            int index2 = triangleIndicesList[j];
                            if (index1 == index2)
                            {
                                notFound = false;
                            }
                            else
                            {
                                ++j;
                            }
                        }

				        if (!notFound)
				        {
					        int tri = j / 3;
                            int vert = j % 3;
                            int srcVert = triangles[tri].vertexNum[vert];
                            int srcOffs = triangles[tri].tSpacesOffset;
                            int dstVert = triangles[t].vertexNum[i];
                            int dstOffs = triangles[t].tSpacesOffset;

                            // copy tspace
                            tSpaces[dstOffs + dstVert] = tSpaces[srcOffs + srcVert];
				        }
			        }
		        }
	        }

	        // deal with degenerate quads with one good triangle
	        for (int t = 0; t < numTriangles; t++)
            {
                // this triangle belongs to a quad where the
                // other triangle is degenerate
                if ((triangles[t].flag & AlgorithmFlags.QuadOneDegenTri) != 0)
                {
                    Vector3 dstP;
                    bool notFound;
                    int[] vertexNumbers = triangles[t].vertexNum;
                    int flag = (1 << vertexNumbers[0]) | (1 << vertexNumbers[1]) | (1 << vertexNumbers[2]);
                    int missingIndex = 0;
                    if ((flag & 2) == 0)
                    {
                        missingIndex = 1;
                    }
                    else if ((flag & 4) == 0)
                    {
                        missingIndex = 2;
                    }
                    else if ((flag & 8) == 0)
                    {
                        missingIndex = 3;
                    }

                    int orgFace = triangles[t].orgFaceNumber;
                    dstP = ctx.positions[MakeIndex(orgFace, missingIndex)];
                    notFound = true;
                    int i = 0;
                    while (notFound && i < 3)
                    {
                        int vertex = vertexNumbers[i];
                        Vector3 vSrcP = ctx.positions[MakeIndex(orgFace, vertex)];
                        if (Equals(vSrcP, dstP))
                        {
                            int offset = triangles[t].tSpacesOffset;
                            tSpaces[offset + missingIndex] = tSpaces[offset + vertex];
                            notFound = false;
                        }
                        else
                        {
                            ++i;
                        }
                    }
                }
            }
        }

        private static float CalcTexArea(Context ctx, ArrayPointerWrapper<int> indices)
        {
            Vector2 t1 = ctx.uvs[indices[0]];
            Vector2 t2 = ctx.uvs[indices[1]];
            Vector2 t3 = ctx.uvs[indices[2]];
            float t21x = t2.X - t1.X;
            float t21y = t2.Y - t1.Y;
            float t31x = t3.X - t1.X;
            float t31y = t3.Y - t1.Y;
            float signedAreaSTx2 = (t21x * t31y) - (t21y * t31x);
            return signedAreaSTx2 < 0 ? (-signedAreaSTx2) : signedAreaSTx2;
        }

        private static void InitTriInfo(Triangle[] triangles, int[] triangleIndicesList, Context ctx, int numTriangles)
        {

            // pTriInfos[f].iFlag is cleared in GenerateInitialVerticesIndexList() which is called before this function.

            // generate neighbor info list
            for (int f = 0; f < numTriangles; f++)
            {
                for (int i = 0; i < 3; i++)
		        {
			        triangles[f].faceNeighbours[i] = -1;
			        triangles[f].assignedGroups[i] = null;

                    triangles[f].os.X = 0.0f;
                    triangles[f].os.Y = 0.0f;
                    triangles[f].os.Z = 0.0f;
                    triangles[f].ot.X = 0.0f;
                    triangles[f].ot.Y = 0.0f;
                    triangles[f].ot.Z = 0.0f;
			        triangles[f].magS = 0;
			        triangles[f].magT = 0;

			        // assumed bad
			        triangles[f].flag |= AlgorithmFlags.GroupWithAny;
		        }
            }

            // evaluate first order derivatives
            for (int f = 0; f < numTriangles; f++)
	        {
		        // initial values
		        Vector3 v1 = ctx.positions[triangleIndicesList[(f * 3) + 0]];
                Vector3 v2 = ctx.positions[triangleIndicesList[(f * 3) + 1]];
                Vector3 v3 = ctx.positions[triangleIndicesList[(f * 3) + 2]];
                Vector2 t1 = ctx.uvs[triangleIndicesList[(f * 3) + 0]];
                Vector2 t2 = ctx.uvs[triangleIndicesList[(f * 3) + 1]];
                Vector2 t3 = ctx.uvs[triangleIndicesList[(f * 3) + 2]];

                float t21x = t2.X - t1.X;
                float t21y = t2.Y - t1.Y;
                float t31x = t3.X - t1.X;
                float t31y = t3.Y - t1.Y;
                Vector3 d1 = Vector3.Subtract(v2, v1);
                Vector3 d2 = Vector3.Subtract(v3, v1);

                float signedAreaSTx2 = (t21x * t31y) - (t21y * t31x);
                //assert(fSignedAreaSTx2!=0);
                Vector3 vOs = Vector3.Subtract(Vector3.Multiply(t31y, d1), Vector3.Multiply(t21y, d2));   // eq 18
                Vector3 vOt = Vector3.Add(Vector3.Multiply(-t31x, d1), Vector3.Multiply(t21x, d2)); // eq 19

                triangles[f].flag |= (signedAreaSTx2 > 0 ? AlgorithmFlags.OrientPreserving : 0);

		        if (NotZero(signedAreaSTx2))
		        {
			        float absArea = MathF.Abs(signedAreaSTx2);
                    float lenOs = vOs.Length();
                    float lenOt = vOt.Length();
                    float s = (triangles[f].flag & AlgorithmFlags.OrientPreserving) == 0 ? (-1.0f) : 1.0f;
                    if (NotZero(lenOs))
                    {
                        triangles[f].os = Vector3.Multiply(s / lenOs, vOs);
                    }

                    if (NotZero(lenOt))
                    {
                        triangles[f].ot = Vector3.Multiply(s / lenOt, vOt);
                    }

                    // evaluate magnitudes prior to normalization of vOs and vOt
                    triangles[f].magS = lenOs / absArea;
			        triangles[f].magT = lenOt / absArea;

                    // if this is a good triangle
                    if (NotZero(triangles[f].magS) && NotZero(triangles[f].magT))
                    {
                        triangles[f].flag &= (~AlgorithmFlags.GroupWithAny);
                    }
                }
	        }

            // force otherwise healthy quads to a fixed orientation
            int t = 0;
	        while (t < (numTriangles - 1))
            {
                int faceA = triangles[t].orgFaceNumber;
                int faceB = triangles[t + 1].orgFaceNumber;
                if (faceA == faceB) // this is a quad
                {
                    bool isDegenerateA = (triangles[t].flag & AlgorithmFlags.MarkDegenerate) != 0;
                    bool isDegenerateB = (triangles[t + 1].flag & AlgorithmFlags.MarkDegenerate) != 0;

                    // bad triangles should already have been removed by
                    // DegenPrologue(), but just in case check bIsDeg_a and bIsDeg_a are false
                    if (!isDegenerateA && !isDegenerateB)
                    {
                        bool orientA = (triangles[t].flag & AlgorithmFlags.OrientPreserving) != 0;
                        bool orientB = (triangles[t + 1].flag & AlgorithmFlags.OrientPreserving) != 0;
                        // if this happens the quad has extremely bad mapping!!
                        if (orientA != orientB)
                        {
                            //printf("found quad with bad mapping\n");
                            bool chooseOrientFirstTri = false;
                            if ((triangles[t + 1].flag & AlgorithmFlags.GroupWithAny) != 0)
                            {
                                chooseOrientFirstTri = true;
                            }
                            else if (CalcTexArea(ctx, new ArrayPointerWrapper<int>(triangleIndicesList, (t * 3) + 0)) >= CalcTexArea(ctx, new ArrayPointerWrapper<int>(triangleIndicesList, ((t + 1) * 3) + 0)))
                            {
                                chooseOrientFirstTri = true;
                            }

                            // force match
                            {
                                int t0 = chooseOrientFirstTri ? t : (t + 1);
                                int t1 = chooseOrientFirstTri ? (t + 1) : t;
                                triangles[t1].flag &= (~AlgorithmFlags.OrientPreserving);    // clear first
                                triangles[t1].flag |= (triangles[t0].flag & AlgorithmFlags.OrientPreserving);   // copy bit
                            }
                        }
                    }

                    t += 2;
                }
                else
                {
                    ++t;
                }
            }

            Edge[] pEdges = new Edge[numTriangles * 3];
            for (int j = 0; j < pEdges.Length; ++j)
            {
                pEdges[j] = new Edge();
            }

            BuildNeighborsFast(triangles, pEdges, triangleIndicesList, numTriangles);
        }

        private static void QuickSortEdges(Edge[] sortBuffer, int left, int right, int channel, uint seed)
        {
            uint t;
            int l, r, n, index, mid;

            // early out
            Edge tmp;
            int elems = right - left + 1;
            if (elems < 2)
            {
                return;
            }
            else if (elems == 2)
	        {
		        if (sortBuffer[left][channel] > sortBuffer[right][channel])
		        {
			        tmp = sortBuffer[left];
			        sortBuffer[left] = sortBuffer[right];
			        sortBuffer[right] = tmp;
		        }

		        return;
	        }

            // Random
            t = seed & 31;
            t = (seed << (int)t) | (seed >> (32 - (int)t));
            seed = seed + t + 3;
            // Random end

            l = left;
            r = right;
            n = r - l + 1;
            index = (int)(seed % n);

            mid = sortBuffer[index + l][channel];

            do
            {
                while (sortBuffer[l][channel] < mid)
                {
                    ++l;
                }

                while (sortBuffer[r][channel] > mid)
                {
                    --r;
                }

                if (l <= r)
                {
                    tmp = sortBuffer[l];
                    sortBuffer[l] = sortBuffer[r];
                    sortBuffer[r] = tmp;
                    ++l; --r;
                }
            }
            while (l <= r);

            if (left < r)
            {
                QuickSortEdges(sortBuffer, left, r, channel, seed);
            }

            if (l < right)
            {
                QuickSortEdges(sortBuffer, l, right, channel, seed);
            }
        }

        private static void GetEdge(ref int index0, ref int index1, ref int edgenum, ArrayPointerWrapper<int> indices, int i0, int i1)
        {
            // test if first index is on the edge
            if (indices[0] == i0 || indices[0] == i1)
	        {
                // test if second index is on the edge
                if (indices[1] == i0 || indices[1] == i1)
		        {
                    edgenum = 0;    // first edge
                    index0 = indices[0];
                    index1 = indices[1];
		        }
		        else
		        {
                    edgenum = 2;    // third edge
                    index0 = indices[2];
                    index1 = indices[0];
		        }
	        }
            else
            {
                // only second and third index is on the edge
                edgenum = 1; // second edge
                index0 = indices[1];
                index1 = indices[2];
            }
        }

        private static void BuildNeighborsFast(Triangle[] triangles, Edge[] edges, int[] triangleIndicesList, int numTriangles)
        {
            // build array of edges
            uint seed = 39871946;                // could replace with a random seed?
            for (int f = 0; f < numTriangles; f++)
            {
                for (int i = 0; i < 3; i++)
		        {
                    int i0 = triangleIndicesList[(f * 3) + i];
                    int i1 = triangleIndicesList[(f * 3) + (i < 2 ? (i + 1) : 0)];
                    edges[(f * 3) + i].i0 = i0 < i1 ? i0 : i1;          // put minimum index in i0
                    edges[(f * 3) + i].i1 = !(i0 < i1) ? i0 : i1;        // put maximum index in i1
                    edges[(f * 3) + i].f = f;                         // record face number
                }
            }

            // sort over all edges by i0, this is the pricy one.
            QuickSortEdges(edges, 0, (numTriangles * 3) - 1, 0, seed);    // sort channel 0 which is i0

            // sub sort over i1, should be fast.
            // could replace this with a 64 bit int sort over (i0,i1)
            // with i0 as msb in the quicksort call above.
            int entries = numTriangles*3;
	        int curStartIndex = 0;
            for (int i = 1; i < entries; i++)
	        {
		        if (edges[curStartIndex].i0 != edges[i].i0)
		        {
			        int l = curStartIndex;
                    int r = i - 1;
                    //const int iElems = i-iL;
                    curStartIndex = i;
			        QuickSortEdges(edges, l, r, 1, seed);   // sort channel 1 which is i1
                }
	        }

	        // sub sort over f, which should be fast.
	        // this step is to remain compliant with BuildNeighborsSlow() when
	        // more than 2 triangles use the same edge (such as a butterfly topology).
	        curStartIndex = 0;
            for (int i = 1; i < entries; i++)
            {
                if (edges[curStartIndex].i0 != edges[i].i0 || edges[curStartIndex].i1 != edges[i].i1)
                {
                    int l = curStartIndex;
                    int r = i - 1;
                    //const int iElems = i-iL;
                    curStartIndex = i;
                    QuickSortEdges(edges, l, r, 2, seed);   // sort channel 2 which is f
                }
            }

            // pair up, adjacent triangles
            for (int i = 0; i < entries; i++)
            {
                int i0 = edges[i].i0;
                int i1 = edges[i].i1;
                int f = edges[i].f;
                bool unassignedA;
                int i0A = 0, i1A = 0;
                int edgenumA = 0, edgenumB = 0;   // 0,1 or 2
                GetEdge(ref i0A, ref i1A, ref edgenumA, new ArrayPointerWrapper<int>(triangleIndicesList, f * 3), i0, i1); // resolve index ordering and edge_num
                unassignedA = triangles[f].faceNeighbours[edgenumA] == -1;
                if (unassignedA)
                {
                    // get true index ordering
                    int j = i + 1, t;
                    bool notFound = true;
                    while (j < entries && i0 == edges[j].i0 && i1 == edges[j].i1 && notFound)
                    {
                        bool unassignedB;
                        int i0B = 0, i1B = 0;
                        t = edges[j].f;
                        // flip i0_B and i1_B
                        GetEdge(ref i1B, ref i0B, ref edgenumB, new ArrayPointerWrapper<int>(triangleIndicesList, t * 3), edges[j].i0, edges[j].i1); // resolve index ordering and edge_num                                                                        //assert(!(i0_A==i1_B && i1_A==i0_B));
                        unassignedB = triangles[t].faceNeighbours[edgenumB] == -1;
                        if (i0A == i0B && i1A == i1B && unassignedB)
                        {
                            notFound = false;
                        }
                        else
                        {
                            ++j;
                        }
                    }

                    if (!notFound)
                    {
                        int t1 = edges[j].f;
                        triangles[f].faceNeighbours[edgenumA] = t1;
                        triangles[t1].faceNeighbours[edgenumB] = f;
                    }
                }
            }
        }

        private static void AddTriToGroup(Group group, int triangleIndex)
        {

            group.faceIndices[group.numFaces] = triangleIndex;
	        ++group.numFaces;
        }

        private static bool AssignRecur(int[] triangleIndicesList, Triangle[] triangles, int triangleIndex, Group group)
        {
	        Triangle triangle = triangles[triangleIndex];

            // track down vertex
            int vertRep = group.vertexRepresentitive;
            ArrayPointerWrapper<int> verts = new ArrayPointerWrapper<int>(triangleIndicesList, (3 * triangleIndex) + 0);
            int i = -1;
            if (verts[0] == vertRep)
            {
                i = 0;
            }
            else if (verts[1] == vertRep)
            {
                i = 1;
            }
            else if (verts[2] == vertRep)
            {
                i = 2;
            }

            // early out
            if (triangle.assignedGroups[i] == group)
            {
                return true;
            }
            else if (triangle.assignedGroups[i] != null)
            {
                return false;
            }

            if ((triangle.flag & AlgorithmFlags.GroupWithAny) != 0)
	        {
		        // first to group with a group-with-anything triangle
		        // determines it's orientation.
		        // This is the only existing order dependency in the code!!
		        if (triangle.assignedGroups[0] == null &&
			        triangle.assignedGroups[1] == null &&
			        triangle.assignedGroups[2] == null)
		        {
			        triangle.flag &= (~AlgorithmFlags.OrientPreserving);
                    triangle.flag |= (group.orientPreservering? AlgorithmFlags.OrientPreserving : 0);
                }
            }

            bool orient = (triangle.flag & AlgorithmFlags.OrientPreserving) != 0;
            if (orient != group.orientPreservering)
            {
                return false;
            }

            AddTriToGroup(group, triangleIndex);
            triangle.assignedGroups[i] = group;

            int neighbourIndexL = triangle.faceNeighbours[i];
            int neighbourIndexR = triangle.faceNeighbours[i > 0 ? (i - 1) : 2];
            if (neighbourIndexL >= 0)
            {
                AssignRecur(triangleIndicesList, triangles, neighbourIndexL, group);
            }

            if (neighbourIndexR >= 0)
            {
                AssignRecur(triangleIndicesList, triangles, neighbourIndexR, group);
            }

            return true;
        }

        private static int Build4RuleGroups(Triangle[] triangles, Group[] groups, int[] groupTrianglesBuffer, int[] triangleIndicesList, int numTriangles)
        {
            int numActiveGroups = 0;
            int offset = 0;
            for (int f = 0; f < numTriangles; f++)
	        {
                for (int i = 0; i < 3; i++)
		        {
			        // if not assigned to a group
			        if ((triangles[f].flag & AlgorithmFlags.GroupWithAny)==0 && triangles[f].assignedGroups[i] == null)
			        {
                        int neighbourIndexL, neighbourindexR;
                        int vertexIndex = triangleIndicesList[(f * 3) + i];
                        triangles[f].assignedGroups[i] = groups[numActiveGroups];
				        triangles[f].assignedGroups[i].vertexRepresentitive = vertexIndex;
                        triangles[f].assignedGroups[i].orientPreservering = (triangles[f].flag & AlgorithmFlags.OrientPreserving) != 0;
                        triangles[f].assignedGroups[i].numFaces = 0;
				        triangles[f].assignedGroups[i].faceIndices = new ArrayPointerWrapper<int>(groupTrianglesBuffer, offset);
				        ++numActiveGroups;

                        AddTriToGroup(triangles[f].assignedGroups[i], f);
                        neighbourIndexL = triangles[f].faceNeighbours[i];
                        neighbourindexR = triangles[f].faceNeighbours[i > 0 ? (i - 1) : 2];
                        if (neighbourIndexL >= 0) // neighbor
                        {
					        AssignRecur(triangleIndicesList, triangles, neighbourIndexL, triangles[f].assignedGroups[i]);
				        }

				        if (neighbourindexR >= 0) // neighbor
				        {
					        AssignRecur(triangleIndicesList, triangles, neighbourindexR, triangles[f].assignedGroups[i]);
				        }

                        // update offset
                        offset += triangles[f].assignedGroups[i].numFaces;
                        // since the groups are disjoint a triangle can never
                        // belong to more than 3 groups. Subsequently something
                        // is completely screwed if this assertion ever hits.
			        }
		        }
	        }

	        return numActiveGroups;
        }

        [Flags]
        private enum AlgorithmFlags
        {
            MarkDegenerate = 1,
            QuadOneDegenTri = 2,
            GroupWithAny = 4,
            OrientPreserving = 8
        }

        private struct Edge
        {
            public int i0;
            public int i1;
            public int f;

            public int this[int i]
            {
                readonly get => i == 0 ? this.i0 : i == 1 ? this.i1 : this.f;
                set
                {
                    switch (i)
                    {
                        case 0:
                        {
                            this.i0 = value; 
                            break;
                        }

                        case 1:
                        {
                            this.i1 = value;
                            break;
                        }

                        default:
                        {
                            this.f = value;
                            break;
                        }
                    }
                }
            }
        }

        private static void QuickSort(int[] sortBuffer, int left, int right, uint seed)
        {
            int l, r, n, index, mid, temp;

            // Random
            uint t = seed & 31;
            t = (seed << (int)t) | (seed >> (32 - (int)t));
            seed = seed + t + 3;
            // Random end

            l = left; r = right;
            n = r - l + 1;
            index = (int)(seed % n);
            mid = sortBuffer[index + l];
            do
            {
                while (sortBuffer[l] < mid)
                {
                    ++l;
                }

                while (sortBuffer[r] > mid)
                {
                    --r;
                }

                if (l <= r)
                {
                    temp = sortBuffer[l];
                    sortBuffer[l] = sortBuffer[r];
                    sortBuffer[r] = temp;
                    ++l; --r;
                }
            }
            while (l <= r);

            if (left < r)
            {
                QuickSort(sortBuffer, left, r, seed);
            }

            if (l < right)
            {
                QuickSort(sortBuffer, l, right, seed);
            }
        }

        private static bool CompareSubGroups(SubGroup groupA, SubGroup groupB)
        {
            bool stillSame = true;
            int i = 0;
            if (groupA.numFaces != groupB.numFaces)
            {
                return false;
            }

            while (i < groupA.numFaces && stillSame)
	        {
                stillSame = groupA.triMembers[i] == groupB.triMembers[i];
                if (stillSame)
                {
                    ++i;
                }
            }

	        return stillSame;
        }

        private static TSpace EvalTspace(int[] faceIndices, int numFaces, int[] triangleIndicesList, Triangle[] triangles,
                          Context ctx, int vertexRepresentitive)
        {

            TSpace res = new TSpace();
            float fAngleSum = 0;
            res.os = Vector3.Zero;
            res.ot = Vector3.Zero;
            res.magT = res.magS = 0;
            for (int face = 0; face < numFaces; face++)
	        {
		        int f = faceIndices[face];

                // only valid triangles get to add their contribution
                if ((triangles[f].flag & AlgorithmFlags.GroupWithAny) == 0)
		        {
                    float fCos, fAngle, fMagS, fMagT;
                    int i = -1;
                    if (triangleIndicesList[(3 * f) + 0] == vertexRepresentitive)
                    {
                        i = 0;
                    }
                    else if (triangleIndicesList[(3 * f) + 1] == vertexRepresentitive)
                    {
                        i = 1;
                    }
                    else if (triangleIndicesList[(3 * f) + 2] == vertexRepresentitive)
                    {
                        i = 2;
                    }


                    // project
                    int index = triangleIndicesList[(3 * f) + i];
			        Vector3 n = ctx.normals[index];
                    Vector3 vOs = Vector3.Subtract(triangles[f].os, Vector3.Multiply(Vector3.Dot(n, triangles[f].os), n));
			        Vector3 vOt = Vector3.Subtract(triangles[f].ot, Vector3.Multiply(Vector3.Dot(n, triangles[f].ot), n));
                    if (NotZero(vOs))
                    {
                        vOs = Vector3.Normalize(vOs);
                    }

                    if (NotZero(vOt))
                    {
                        vOt = Vector3.Normalize(vOt);
                    }

                    int i2 = triangleIndicesList[(3 * f) + (i < 2 ? (i + 1) : 0)];
                    int i1 = triangleIndicesList[(3 * f) + i];
                    int i0 = triangleIndicesList[(3 * f) + (i > 0 ? (i - 1) : 2)];

			        Vector3 p0 = ctx.positions[i0];
                    Vector3 p1 = ctx.positions[i1];
                    Vector3 p2 = ctx.positions[i2];
                    Vector3 v1 = Vector3.Subtract(p0, p1);
                    Vector3 v2 = Vector3.Subtract(p2, p1);

                    // project
                    v1 = Vector3.Subtract(v1, Vector3.Multiply(Vector3.Dot(n, v1), n));
                    if (NotZero(v1))
                    {
                        v1 = Vector3.Normalize(v1);
                    }

                    v2 = Vector3.Subtract(v2, Vector3.Multiply(Vector3.Dot(n, v2), n));
                    if (NotZero(v2))
                    {
                        v2 = Vector3.Normalize(v2);
                    }

                    // weight contribution by the angle
                    // between the two edge vectors
                    fCos = Vector3.Dot(v1, v2);
                    fCos = fCos > 1 ? 1 : (fCos < (-1) ? (-1) : fCos);
			        fAngle = MathF.Acos(fCos);
                    fMagS = triangles[f].magS;
			        fMagT = triangles[f].magT;

                    res.os = Vector3.Add(res.os, Vector3.Multiply(fAngle, vOs));
                    res.ot = Vector3.Add(res.ot, Vector3.Multiply(fAngle, vOt));
                    res.magS += (fAngle * fMagS);
                    res.magT += (fAngle * fMagT);
			        fAngleSum += fAngle;
		        }
            }

            // normalize
            if (NotZero(res.os))
            {
                res.os = Vector3.Normalize(res.os);
            }

            if (NotZero(res.ot))
            {
                res.ot = Vector3.Normalize(res.ot);
            }

            if (fAngleSum > 0)
            {
                res.magS /= fAngleSum;
                res.magT /= fAngleSum;
            }

            return res;
        }

        private static bool GenerateTSpaces(TSpace[] tSpaces, Triangle[] triangles, Group[] groups,
                             int numActiveGroups, int[] triangleIndicesList, float thresholdCos,
                             Context ctx)
        {

            int maxFaces = 0;
            for (int g = 0; g < numActiveGroups; g++)
            {
                if (maxFaces < groups[g].numFaces)
                {
                    maxFaces = groups[g].numFaces;
                }
            }

            if (maxFaces == 0)
            {
                return true;
            }

            // make initial allocations
            TSpace[] subGroupTspace = new TSpace[maxFaces];
            for (int j = 0; j < maxFaces; ++j)
            {
                subGroupTspace[j] = new TSpace();
            }

            SubGroup[] uniSubGroups = new SubGroup[maxFaces];
            for (int j = 0; j < maxFaces; ++j)
            {
                uniSubGroups[j] = new SubGroup();
            }

            int[] tempMembers = new int[maxFaces];
            int uniqueTspaces = 0;
            for (int g = 0; g < numActiveGroups; g++)
	        {
		        Group group = groups[g];
                int uniqueSubGroups = 0;
                for (int i = 0; i < group.numFaces; i++)  // triangles
                {
			        int f = group.faceIndices[i];  // triangle number
                    int index = -1;
                    SubGroup tempGroup = new SubGroup();
                    bool found;
                    Vector3 n, vOs, vOt;
                    if (triangles[f].assignedGroups[0] == group)
                    {
                        index = 0;
                    }
                    else if (triangles[f].assignedGroups[1] == group)
                    {
                        index = 1;
                    }
                    else if (triangles[f].assignedGroups[2] == group)
                    {
                        index = 2;
                    }

                    int vertIndex = triangleIndicesList[(f * 3) + index];

                    // is normalized already
                    n = ctx.normals[vertIndex];

                    // project
                    vOs = Vector3.Subtract(triangles[f].os, Vector3.Multiply(Vector3.Dot(n, triangles[f].os), n));
			        vOt = Vector3.Subtract(triangles[f].ot, Vector3.Multiply(Vector3.Dot(n, triangles[f].ot), n));
                    if (NotZero(vOs))
                    {
                        vOs = Vector3.Normalize(vOs);
                    }

                    if (NotZero(vOt))
                    {
                        vOt = Vector3.Normalize(vOt);
                    }

                    // original face number
                    int face1 = triangles[f].orgFaceNumber;
			        int members = 0;
                    for (int j = 0; j < group.numFaces; j++)
			        {
				        int t = group.faceIndices[j];  // triangle number
                        int face2 = triangles[t].orgFaceNumber;

                        // project
                        Vector3 vOs2 = Vector3.Subtract(triangles[t].os, Vector3.Multiply(Vector3.Dot(n, triangles[t].os), n));
                        Vector3 vOt2 = Vector3.Subtract(triangles[t].ot, Vector3.Multiply(Vector3.Dot(n, triangles[t].ot), n));
                        if (NotZero(vOs2))
                        {
                            vOs2 = Vector3.Normalize(vOs2);
                        }

                        if (NotZero(vOt2))
                        {
                            vOt2 = Vector3.Normalize(vOt2);
                        }

                        bool any = ((triangles[f].flag | triangles[t].flag) & AlgorithmFlags.GroupWithAny) != 0;
                        // make sure triangles which belong to the same quad are joined.
                        bool sameOrgFace = face1 == face2;
                        float cosS = Vector3.Dot(vOs, vOs2);
                        float cosT = Vector3.Dot(vOt, vOt2);
                        if (any || sameOrgFace || (cosS > thresholdCos && cosT > thresholdCos))
                        {
                            tempMembers[members++] = t;
                        }
			        }

			        // sort pTmpMembers
			        tempGroup.numFaces = members;
                    tempGroup.triMembers = tempMembers;
                    if (members > 1)
                    {
                        // could replace with a random seed?
                        QuickSort(tempMembers, 0, members - 1, 39871946);
                    }

                    // look for an existing match
                    found = false;
                    int l = 0;
                    while (l < uniqueSubGroups && !found)
                    {
                        found = CompareSubGroups(tempGroup, uniSubGroups[l]);
                        if (!found)
                        {
                            ++l;
                        }
                    }

                    // assign tangent space index
                    //piTempTangIndices[f*3+index] = iUniqueTspaces+l;

                    // if no match was found we allocate a new subgroup
                    if (!found)
                    {
                        // insert new subgroup
                        int[] indices = new int[members];
                        uniSubGroups[uniqueSubGroups].numFaces = members;
                        uniSubGroups[uniqueSubGroups].triMembers = indices;
                        Array.Copy(tempGroup.triMembers, indices, members);
                        subGroupTspace[uniqueSubGroups] =
                        EvalTspace(tempGroup.triMembers, members, triangleIndicesList, triangles, ctx, group.vertexRepresentitive);
                        ++uniqueSubGroups;
                    }

                    // output tspace
                    int offset = triangles[f].tSpacesOffset;
                    int vertex = triangles[f].vertexNum[index];
                    TSpace result = tSpaces[offset + vertex];
                    if (result.counter == 1)
                    {
                        result = AvgTSpace(result, subGroupTspace[l]);
                        result.counter = 2;  // update counter
                        result.orient = group.orientPreservering;
                    }
                    else
                    {
                        result = subGroupTspace[l];
                        result.counter = 1;  // update counter
                        result.orient = group.orientPreservering;
                    }
		        }

		        // clean up and offset iUniqueTspaces
                uniqueTspaces += uniqueSubGroups;
	        }

            return true;
        }

        private readonly struct ArrayPointerWrapper<T>
        {
            private readonly T[] arr;
            private readonly int offset;

            public T this[int i]
            {
                get => this.arr[i + this.offset];
                set => this.arr[i + this.offset] = value;
            }

            public ArrayPointerWrapper(T[] arr, int offset)
            {
                this.arr = arr;
                this.offset = offset;
            }
        }

        private class SubGroup
        {
            public int numFaces;
            public int[] triMembers;
        }

        private class Group
        {
            public int numFaces;
            public ArrayPointerWrapper<int> faceIndices;
            public int vertexRepresentitive;
            public bool orientPreservering;
        }

        private class Triangle
        {
            public int[] faceNeighbours;
            public Group[] assignedGroups;

            public Vector3 os = Vector3.Zero;
            public Vector3 ot = Vector3.Zero;
            public float magS;
            public float magT;

            public int orgFaceNumber;
            public AlgorithmFlags flag;
            public int tSpacesOffset;

            public int[] vertexNum;
        }

        private class TSpace
        {
            public Vector3 os = Vector3.Zero;
            public float magS;
            public Vector3 ot = Vector3.Zero;
            public float magT;
            public int counter;
            public bool orient;
        }

        private class TempVert
        {
            public Vector3 vert = Vector3.Zero;
            public int index;
        }
    }
}
