using Antlr4.Runtime.Tree;
using System.Runtime.Intrinsics.X86;
using System;
using VTT.GL;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace VTT.Util
{
    using Antlr4.Runtime.Tree;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Numerics;
    using System.Reflection.Metadata;
    using System.Runtime.Intrinsics.X86;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

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
            int[] triListIn = null;
            int[] groupTrianglesBuffer = null;
            Triangle[] triInfos = null;
            Group[] groups = null;
            TSpace[] tspace = null;
            int nrTSPaces = 0, totTris = 0, degenTriangles = 0, nrMaxGroups = 0;
            int nrActiveGroups = 0, index = 0;
            int nrFaces = indices.Length / 3;
            int nrTrianglesIn = nrFaces, f = 0, t = 0, i = 0;
            bool bRes = false;
            float fThresCos = MathF.Cos((angularThreshold * MathF.PI) / 180.0f);

            if (nrTrianglesIn <= 0)
            {
                result = null;
                return false;
            }

            // allocate memory for an index list
            triListIn = new int[nrTrianglesIn * 3];
            triInfos = new Triangle[nrTrianglesIn];
            for (int i1 = 0; i1 < triInfos.Length; i1++)
            {
                Triangle tri = new Triangle();
                tri.vertexNum = new int[4];
                tri.assignedGroups = new Group[3];
                tri.faceNeighbours = new int[3];
                triInfos[i1] = tri;
            }

            // make an initial triangle --> face index list
            nrTSPaces = GenerateInitialVerticesIndexList(triInfos, triListIn, ctx, nrTrianglesIn);

            // make a welded index list of identical positions and attributes (pos, norm, texc)
            //printf("gen welded index list begin\n");
            GenerateSharedVerticesIndexList(triListIn, ctx, nrTrianglesIn);
            //printf("gen welded index list end\n");

            // Mark all degenerate triangles
            totTris = nrTrianglesIn;
            degenTriangles = 0;
            for (t = 0; t < totTris; t++)
            {
                int i0 = triListIn[t * 3 + 0];
                int i1 = triListIn[t * 3 + 1];
                int i2 = triListIn[t * 3 + 2];
                Vector3 p0 = ctx.positions[i0];
                Vector3 p1 = ctx.positions[i1];
                Vector3 p2 = ctx.positions[i2];
                if (Vector3.Equals(p0, p1) || Vector3.Equals(p0, p2) || Vector3.Equals(p1, p2))  // degenerate
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
            nrMaxGroups = nrTrianglesIn * 3;
            groups = new Group[nrMaxGroups];
            for (int j = 0; j < nrMaxGroups; ++j)
            {
                groups[j] = new Group();
            }

            groupTrianglesBuffer =  new int[nrTrianglesIn * 3];
            //printf("gen 4rule groups begin\n");
            nrActiveGroups = Build4RuleGroups(triInfos, groups, groupTrianglesBuffer, triListIn, nrTrianglesIn);
            //printf("gen 4rule groups end\n");

            //

            tspace = new TSpace[nrTSPaces];

            for (t = 0; t < nrTSPaces; t++)
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
            bRes = GenerateTSpaces(tspace, triInfos, groups, nrActiveGroups, triListIn, fThresCos, ctx);
            //printf("gen tspaces end\n");

            // degenerate quads with one good triangle will be fixed by copying a space from
            // the good triangle to the coinciding vertex.
            // all other degenerate triangles will just copy a space from any good triangle
            // with the same welded index in piTriListIn[].
            DegenEpilogue(tspace, triInfos, triListIn, ctx, nrTrianglesIn, totTris);


            index = 0;
            result = new TangentData[nrFaces * 3];
            for (f = 0; f < nrFaces; f++)
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
                for (i = 0; i < 3; i++)
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
        private static void IndexToData(ref int face, ref int vert, int indexIn)
        {
            vert = indexIn & 3;
            face = indexIn >> 2;
        }

        private static TSpace AvgTSpace(TSpace ts0, TSpace ts1)
        {
            TSpace ts_res = new TSpace();

            if (ts0.magS == ts1.magS && ts0.magT == ts1.magT &&
               Vector3.Equals(ts0.os, ts1.os) && Vector3.Equals(ts0.ot, ts1.ot))
            {
                ts_res.magS = ts0.magS;
                ts_res.magT = ts0.magT;
                ts_res.os = ts0.os;
                ts_res.ot = ts0.ot;
            }
            else
            {
                ts_res.magS = 0.5f * (ts0.magS + ts1.magS);
                ts_res.magT = 0.5f * (ts0.magT + ts1.magT);
                ts_res.os = Vector3.Add(ts0.os, ts1.os);
                ts_res.ot = Vector3.Add(ts0.ot, ts1.ot);
                if (NotZero(ts_res.os))
                {
                    ts_res.os = Vector3.Normalize(ts_res.os);
                }

                if (NotZero(ts_res.ot))
                {
                    ts_res.ot = Vector3.Normalize(ts_res.ot);
                }
            }

            return ts_res;
        }

        private static int GenerateInitialVerticesIndexList(Triangle[] triangles, int[] triList, Context pContext, int numTrianglesIn)
        {
            int tSpacesOffs = 0, f = 0, t = 0;
            int dstTriIndex = 0;
	        for (f = 0; f < pContext.indices.Length / 3; f++)
	        {
		        triangles[dstTriIndex].orgFaceNumber = f;
		        triangles[dstTriIndex].tSpacesOffset = tSpacesOffs;
			    int[] pVerts = triangles[dstTriIndex].vertexNum;
                pVerts[0]=0; pVerts[1]=1; pVerts[2]=2;
                int x = f * 3;
			    triList[dstTriIndex * 3 + 0] = (int)pContext.indices[x + 0];
                triList[dstTriIndex * 3 + 1] = (int)pContext.indices[x + 1];
                triList[dstTriIndex * 3 + 2] = (int)pContext.indices[x + 2];
			    ++dstTriIndex;	// next
		        tSpacesOffs += 3;
	        }

            for (t = 0; t < numTrianglesIn; t++)
            {
                triangles[t].flag = 0;
            }

            return tSpacesOffs;
        }

        private static void GenerateSharedVerticesIndexList(int[] piTriList_in_and_out, Context pContext, int iNrTrianglesIn)
        {

            // Generate bounding box
            int[] piHashTable = null;
            int[] piHashCount = null;
            int[] piHashOffsets = null;
            int[] piHashCount2 = null;
	        TempVert[] pTmpVert = null;
            int i = 0, iChannel = 0, k = 0, e = 0;
            int iMaxCount = 0;
            Vector3 vMin = pContext.positions[0], vMax = vMin, vDim;
            float fMin, fMax;
            for (i = 1; i < (iNrTrianglesIn * 3); i++)
	        {
		        int index = piTriList_in_and_out[i];
                Vector3 vP = pContext.positions[index];
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
            iChannel = 0;
	        fMin = vMin.X; fMax=vMax.X;
            if (vDim.Y > vDim.X && vDim.Y > vDim.Z)
	        {
		        iChannel=1;
		        fMin = vMin.Y;
		        fMax = vMax.Y;
	        }

            else if (vDim.Z > vDim.X)
            {
                iChannel = 2;
                fMin = vMin.Z;
                fMax = vMax.Z;
            }

            // make allocations
            piHashTable = new int[iNrTrianglesIn * 3];
            piHashCount = new int[2048];
            piHashOffsets = new int[2048];
            piHashCount2 = new int[2048];

            // count amount of elements in each cell unit
            for (i = 0; i < (iNrTrianglesIn * 3); i++)
            {
                int index = piTriList_in_and_out[i];
                Vector3 vP = pContext.positions[index];
                float fVal = iChannel == 0 ? vP.X : (iChannel == 1 ? vP.Y : vP.Z);
                int iCell = FindGridCell(fMin, fMax, fVal);
                ++piHashCount[iCell];
            }

            // evaluate start index of each cell.
            piHashOffsets[0] = 0;
            for (k = 1; k < 2048; k++)
            {
                piHashOffsets[k] = piHashOffsets[k - 1] + piHashCount[k - 1];
            }

            // insert vertices
            for (i = 0; i < (iNrTrianglesIn * 3); i++)
            {
                int index = piTriList_in_and_out[i];
                Vector3 vP = pContext.positions[index];
                float fVal = iChannel == 0 ? vP.X : (iChannel == 1 ? vP.Y : vP.Z);
                int iCell = FindGridCell(fMin, fMax, fVal);
                piHashTable[piHashOffsets[iCell]] = i;
                ++piHashCount2[iCell];
            }

            // find maximum amount of entries in any hash entry
            iMaxCount = piHashCount[0];
            for (k = 1; k < 2048; k++)
            {
                if (iMaxCount < piHashCount[k])
                {
                    iMaxCount = piHashCount[k];
                }
            }

            pTmpVert = new TempVert[iMaxCount];
            for (int j = 0; j < iMaxCount; ++j)
            {
                pTmpVert[j] = new TempVert();
            }

            // complete the merge
            for (k = 0; k < 2048; k++)
            {
                // extract table of cell k and amount of entries in it
                ArrayPointerWrapper<int> pTable = new ArrayPointerWrapper<int>(piHashTable, piHashOffsets[k]);
                int iEntries = piHashCount[k];
                if (iEntries < 2)
                {
                    continue;
                }

                if (pTmpVert != null)
                {
                    for (e = 0; e < iEntries; e++)
                    {
                        int j = pTable[e];
                        Vector3 vP = pContext.positions[piTriList_in_and_out[j]];
                        pTmpVert[e].vert.X = vP.X; 
                        pTmpVert[e].vert.Y = vP.Y;
                        pTmpVert[e].vert.Z = vP.Z; 
                        pTmpVert[e].index = j;
                    }

                    MergeVertsFast(piTriList_in_and_out, pTmpVert, pContext, 0, iEntries - 1);
                }
                else
                {
                    MergeVertsSlow(piTriList_in_and_out, pContext, pTable, iEntries);
                }
            }
        }

        private static void MergeVertsFast(int[] piTriList_in_and_out, TempVert[] pTmpVert, Context pContext, int iL_in, int iR_in)
        {
            // make bbox
            int c = 0, l = 0, channel = 0;
            float[] fvMin = new float[3], fvMax = new float[3];
            float dx = 0, dy = 0, dz = 0, fSep = 0;
            for (c = 0; c < 3; c++)
            {
                Vector3 vert = pTmpVert[iL_in].vert;
                fvMin[c] = c == 0 ? vert.X : c == 1 ? vert.Y : vert.Z; 
                fvMax[c] = fvMin[c]; 
            }

            for (l = (iL_in + 1); l <= iR_in; l++)
            {
                for (c = 0; c < 3; c++)
                {
                    Vector3 vert = pTmpVert[l].vert;
                    float fv = c == 0 ? vert.X : c == 1 ? vert.Y : vert.Z;
                    if (fvMin[c] > fv)
                    {
                        fvMin[c] = fv;
                    }

                    if (fvMax[c] < fv)
                    {
                        fvMax[c] = fv;
                    }
                }
	        }

	        dx = fvMax[0] - fvMin[0];
            dy = fvMax[1] - fvMin[1];
            dz = fvMax[2] - fvMin[2];

            channel = 0;
            if (dy > dx && dy > dz)
            {
                channel = 1;
            }
            else if (dz > dx)
            {
                channel = 2;
            }

            fSep = 0.5f * (fvMax[channel] + fvMin[channel]);

            // stop if all vertices are NaNs
            if (float.IsNaN(fSep))
            {
                return;
            }

            // terminate recursion when the separation/average value
            // is no longer strictly between fMin and fMax values.
            if (fSep >= fvMax[channel] || fSep <= fvMin[channel])
            {
                // complete the weld
                for (l = iL_in; l <= iR_in; l++)
                {
                    int i = pTmpVert[l].index;
                    int index = piTriList_in_and_out[i];
                    Vector3 vP = pContext.positions[index];
                    Vector3 vN = pContext.normals[index];
                    Vector2 vT = pContext.uvs[index];

                    bool bNotFound = true;
                    int l2 = iL_in, i2rec = -1;
                    while (l2 < l && bNotFound)
                    {
                        int i2 = pTmpVert[l2].index;
                        int index2 = piTriList_in_and_out[i2];
                        Vector3 vP2 = pContext.positions[index2];
                        Vector3 vN2 = pContext.normals[index2];
                        Vector2 vT2 = pContext.uvs[index2];
                        i2rec = i2;

                        //if (vP==vP2 && vN==vN2 && vT==vT2)
                        if (vP.X == vP2.X && vP.Y == vP2.Y && vP.Z == vP2.Z &&
                            vN.X == vN2.X && vN.Y == vN2.Y && vN.Z == vN2.Z &&
                            vT.X == vT2.X && vT.Y == vT2.Y)
                        {
                            bNotFound = false;
                        }
                        else
                        {
                            ++l2;
                        }
                    }

                    // merge if previously found
                    if (!bNotFound)
                    {
                        piTriList_in_and_out[i] = piTriList_in_and_out[i2rec];
                    }
                }
            }
            else
            {
                int iL = iL_in, iR = iR_in;
                // separate (by fSep) all points between iL_in and iR_in in pTmpVert[]
                while (iL < iR)
                {
                    bool bReadyLeftSwap = false, bReadyRightSwap = false;
                    while ((!bReadyLeftSwap) && iL < iR)
                    {
                        Vector3 vert = pTmpVert[iL].vert;
                        bReadyLeftSwap = !((channel == 0 ? vert.X : channel == 1 ? vert.Y : vert.Z) < fSep);
                        if (!bReadyLeftSwap)
                        {
                            ++iL;
                        }
                    }
                    while ((!bReadyRightSwap) && iL < iR)
                    {
                        Vector3 vert = pTmpVert[iR].vert;
                        bReadyRightSwap = (channel == 0 ? vert.X : channel == 1 ? vert.Y : vert.Z) < fSep;
                        if (!bReadyRightSwap)
                        {
                            --iR;
                        }
                    }

                    if (bReadyLeftSwap && bReadyRightSwap)
                    {
                        TempVert sTmp = pTmpVert[iL];
                        pTmpVert[iL] = pTmpVert[iR];
                        pTmpVert[iR] = sTmp;
                        ++iL; --iR;
                    }
                }

                if (iL == iR)
                {
                    Vector3 vert = pTmpVert[iR].vert;
                    bool bReadyRightSwap = (channel == 0 ? vert.X : channel == 1 ? vert.Y : vert.Z) < fSep;
                    if (bReadyRightSwap)
                    {
                        ++iL;
                    }
                    else
                    {
                        --iR;
                    }
                }

                // only need to weld when there is more than 1 instance of the (x,y,z)
                if (iL_in < iR)
                {
                    MergeVertsFast(piTriList_in_and_out, pTmpVert, pContext, iL_in, iR);    // weld all left of fSep
                }

                if (iL < iR_in)
                {
                    MergeVertsFast(piTriList_in_and_out, pTmpVert, pContext, iL, iR_in);    // weld all right of (or equal to) fSep
                }
            }
        }

        private static void MergeVertsSlow(int[] piTriList_in_and_out, Context pContext, ArrayPointerWrapper<int> pTable, int iEntries)
        {
            // this can be optimized further using a tree structure or more hashing.
            int e = 0;
            for (e = 0; e < iEntries; e++)
	        {
		        int i = pTable[e];
                int index = piTriList_in_and_out[i];
                Vector3 vP = pContext.positions[index];
                Vector3 vN = pContext.normals[index];
                Vector2 vT = pContext.uvs[index];

                bool bNotFound = true;
                int e2 = 0, i2rec = -1;
                while (e2 < e && bNotFound)
		        {
			        int i2 = pTable[e2];
                    int index2 = piTriList_in_and_out[i2];
                    Vector3 vP2 = pContext.positions[index2];
                    Vector3 vN2 = pContext.normals[index2];
                    Vector2 vT2 = pContext.uvs[index2];
                    i2rec = i2;

                    if (Vector3.Equals(vP, vP2) && Vector3.Equals(vN, vN2) && Vector2.Equals(vT, vT2))
                    {
                        bNotFound = false;
                    }
                    else
                    {
                        ++e2;
                    }
                }

                if (!bNotFound)
                {
                    piTriList_in_and_out[i] = piTriList_in_and_out[i2rec];
                }
            }
        }

        private static void DegenPrologue(Triangle[] pTriInfos, int[] piTriList_out, int iNrTrianglesIn, int iTotTris)
        {

            int iNextGoodTriangleSearchIndex = -1;
            bool bStillFindingGoodOnes;

            // locate quads with only one good triangle
            int t = 0;
            while (t < (iTotTris - 1))
	        {
		        int iFO_a = pTriInfos[t].orgFaceNumber;
                int iFO_b = pTriInfos[t + 1].orgFaceNumber;
		        if (iFO_a==iFO_b)	// this is a quad
		        {
                    bool bIsDeg_a = (pTriInfos[t].flag & AlgorithmFlags.MarkDegenerate) != 0;
                    bool bIsDeg_b = (pTriInfos[t + 1].flag & AlgorithmFlags.MarkDegenerate) != 0;
			        if (bIsDeg_a != bIsDeg_b)
			        {
                        pTriInfos[t].flag |= AlgorithmFlags.QuadOneDegenTri;
				        pTriInfos[t + 1].flag |= AlgorithmFlags.QuadOneDegenTri;
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
	        iNextGoodTriangleSearchIndex = 1;
            t = 0;
            bStillFindingGoodOnes = true;
            while (t < iNrTrianglesIn && bStillFindingGoodOnes)
            {
                bool bIsGood = (pTriInfos[t].flag & AlgorithmFlags.MarkDegenerate) == 0;
                if (bIsGood)
                {
                    if (iNextGoodTriangleSearchIndex < (t + 2))
                    {
                        iNextGoodTriangleSearchIndex = t + 2;
                    }
                }
                else
                {
                    int t0, t1;
                    // search for the first good triangle.
                    bool bJustADegenerate = true;
                    while (bJustADegenerate && iNextGoodTriangleSearchIndex < iTotTris)
                    {
                        bool bIsGood1 = (pTriInfos[iNextGoodTriangleSearchIndex].flag & AlgorithmFlags.MarkDegenerate) == 0 ? true : false;
                        if (bIsGood1)
                        {
                            bJustADegenerate = false;
                        }
                        else
                        {
                            ++iNextGoodTriangleSearchIndex;
                        }
                    }

                    t0 = t;
                    t1 = iNextGoodTriangleSearchIndex;
                    ++iNextGoodTriangleSearchIndex;

                    // swap triangle t0 and t1
                    if (!bJustADegenerate)
                    {
                        int i = 0;
                        for (i = 0; i < 3; i++)
                        {
                            int index = piTriList_out[t0 * 3 + i];
                            piTriList_out[t0 * 3 + i] = piTriList_out[t1 * 3 + i];
                            piTriList_out[t1 * 3 + i] = index;
                        }

                        Triangle tri_info = pTriInfos[t0];
                        pTriInfos[t0] = pTriInfos[t1];
                        pTriInfos[t1] = tri_info;
                    }
                    else
                    {
                        bStillFindingGoodOnes = false; // this is not supposed to happen
                    }
                }

                if (bStillFindingGoodOnes)
                {
                    ++t;
                }
            }
        }

        private static void DegenEpilogue(TSpace[] psTspace, Triangle[] pTriInfos, int[] piTriListIn, Context pContext, int iNrTrianglesIn, int iTotTris)
        {

            int t = 0, i = 0;
            // deal with degenerate triangles
            // punishment for degenerate triangles is O(N^2)
            for (t = iNrTrianglesIn; t < iTotTris; t++)
	        {
                // degenerate triangles on a quad with one good triangle are skipped
                // here but processed in the next loop
                bool bSkip = (pTriInfos[t].flag & AlgorithmFlags.QuadOneDegenTri) != 0;
		        if (!bSkip)
		        {
                    for (i = 0; i < 3; i++)
			        {
				        int index1 = piTriListIn[t * 3 + i];
                        // search through the good triangles
                        bool bNotFound = true;
                        int j = 0;
                        while (bNotFound && j < (3 * iNrTrianglesIn))
				        {

                            int index2 = piTriListIn[j];
                            if (index1 == index2)
                            {
                                bNotFound = false;
                            }
                            else
                            {
                                ++j;
                            }
                        }

				        if (!bNotFound)
				        {
					        int iTri = j / 3;
                            int iVert = j % 3;
                            int iSrcVert = pTriInfos[iTri].vertexNum[iVert];
                            int iSrcOffs = pTriInfos[iTri].tSpacesOffset;
                            int iDstVert = pTriInfos[t].vertexNum[i];
                            int iDstOffs = pTriInfos[t].tSpacesOffset;

                            // copy tspace
                            psTspace[iDstOffs + iDstVert] = psTspace[iSrcOffs + iSrcVert];
				        }
			        }
		        }
	        }

	        // deal with degenerate quads with one good triangle
	        for (t = 0; t < iNrTrianglesIn; t++)
            {
                // this triangle belongs to a quad where the
                // other triangle is degenerate
                if ((pTriInfos[t].flag & AlgorithmFlags.QuadOneDegenTri) != 0)
                {
                    Vector3 vDstP;
                    int iOrgF = -1;
                    bool bNotFound;
                    int[] pV = pTriInfos[t].vertexNum;
                    int iFlag = (1 << pV[0]) | (1 << pV[1]) | (1 << pV[2]);
                    int iMissingIndex = 0;
                    if ((iFlag & 2) == 0)
                    {
                        iMissingIndex = 1;
                    }
                    else if ((iFlag & 4) == 0)
                    {
                        iMissingIndex = 2;
                    }
                    else if ((iFlag & 8) == 0)
                    {
                        iMissingIndex = 3;
                    }

                    iOrgF = pTriInfos[t].orgFaceNumber;
                    vDstP = pContext.positions[MakeIndex(iOrgF, iMissingIndex)];
                    bNotFound = true;
                    i = 0;
                    while (bNotFound && i < 3)
                    {
                        int iVert = pV[i];
                        Vector3 vSrcP = pContext.positions[MakeIndex(iOrgF, iVert)];
                        if (Vector3.Equals(vSrcP, vDstP))
                        {
                            int iOffs = pTriInfos[t].tSpacesOffset;
                            psTspace[iOffs + iMissingIndex] = psTspace[iOffs + iVert];
                            bNotFound = false;
                        }
                        else
                        {
                            ++i;
                        }
                    }
                }
            }
        }

        private static float CalcTexArea(Context pContext, ArrayPointerWrapper<int> indices)
        {
            Vector2 t1 = pContext.uvs[indices[0]];
            Vector2 t2 = pContext.uvs[indices[1]];
            Vector2 t3 = pContext.uvs[indices[2]];
            float t21x = t2.X - t1.X;
            float t21y = t2.Y - t1.Y;
            float t31x = t3.X - t1.X;
            float t31y = t3.Y - t1.Y;
            float fSignedAreaSTx2 = t21x * t31y - t21y * t31x;
            return fSignedAreaSTx2 < 0 ? (-fSignedAreaSTx2) : fSignedAreaSTx2;
        }

        private static void InitTriInfo(Triangle[] pTriInfos, int[] piTriListIn, Context pContext, int iNrTrianglesIn)
        {

            int f = 0, i = 0, t = 0;
            // pTriInfos[f].iFlag is cleared in GenerateInitialVerticesIndexList() which is called before this function.

            // generate neighbor info list
            for (f = 0; f < iNrTrianglesIn; f++)
            {
                for (i=0; i<3; i++)
		        {
			        pTriInfos[f].faceNeighbours[i] = -1;
			        pTriInfos[f].assignedGroups[i] = null;

			        pTriInfos[f].os.X=0.0f; 
                    pTriInfos[f].os.Y=0.0f; 
                    pTriInfos[f].os.Z=0.0f;
			        pTriInfos[f].ot.X=0.0f; 
                    pTriInfos[f].ot.Y=0.0f; 
                    pTriInfos[f].ot.Z=0.0f;
			        pTriInfos[f].magS = 0;
			        pTriInfos[f].magT = 0;

			        // assumed bad
			        pTriInfos[f].flag |= AlgorithmFlags.GroupWithAny;
		        }
            }

            // evaluate first order derivatives
            for (f = 0; f < iNrTrianglesIn; f++)
	        {
		        // initial values
		        Vector3 v1 = pContext.positions[piTriListIn[f * 3 + 0]];
                Vector3 v2 = pContext.positions[piTriListIn[f * 3 + 1]];
                Vector3 v3 = pContext.positions[piTriListIn[f * 3 + 2]];
                Vector2 t1 = pContext.uvs[piTriListIn[f * 3 + 0]];
                Vector2 t2 = pContext.uvs[piTriListIn[f * 3 + 1]];
                Vector2 t3 = pContext.uvs[piTriListIn[f * 3 + 2]];

                float t21x = t2.X - t1.X;
                float t21y = t2.Y - t1.Y;
                float t31x = t3.X - t1.X;
                float t31y = t3.Y - t1.Y;
                Vector3 d1 = Vector3.Subtract(v2, v1);
                Vector3 d2 = Vector3.Subtract(v3, v1);

                float fSignedAreaSTx2 = t21x * t31y - t21y * t31x;
                //assert(fSignedAreaSTx2!=0);
                Vector3 vOs = Vector3.Subtract(Vector3.Multiply(t31y, d1), Vector3.Multiply(t21y, d2));   // eq 18
                Vector3 vOt = Vector3.Add(Vector3.Multiply(-t31x, d1), Vector3.Multiply(t21x, d2)); // eq 19

                pTriInfos[f].flag |= (fSignedAreaSTx2 > 0 ? AlgorithmFlags.OrientPreserving : 0);

		        if (NotZero(fSignedAreaSTx2))
		        {
			        float fAbsArea = MathF.Abs(fSignedAreaSTx2);
                    float fLenOs = vOs.Length();
                    float fLenOt = vOt.Length();
                    float fS = (pTriInfos[f].flag & AlgorithmFlags.OrientPreserving) == 0 ? (-1.0f) : 1.0f;
                    if (NotZero(fLenOs))
                    {
                        pTriInfos[f].os = Vector3.Multiply(fS / fLenOs, vOs);
                    }

                    if (NotZero(fLenOt))
                    {
                        pTriInfos[f].ot = Vector3.Multiply(fS / fLenOt, vOt);
                    }

                    // evaluate magnitudes prior to normalization of vOs and vOt
                    pTriInfos[f].magS = fLenOs / fAbsArea;
			        pTriInfos[f].magT = fLenOt / fAbsArea;

                    // if this is a good triangle
                    if (NotZero(pTriInfos[f].magS) && NotZero(pTriInfos[f].magT))
                    {
                        pTriInfos[f].flag &= (~AlgorithmFlags.GroupWithAny);
                    }
                }
	        }

	        // force otherwise healthy quads to a fixed orientation
	        while (t < (iNrTrianglesIn - 1))
            {
                int iFO_a = pTriInfos[t].orgFaceNumber;
                int iFO_b = pTriInfos[t + 1].orgFaceNumber;
                if (iFO_a == iFO_b) // this is a quad
                {
                    bool bIsDeg_a = (pTriInfos[t].flag & AlgorithmFlags.MarkDegenerate) != 0;
                    bool bIsDeg_b = (pTriInfos[t + 1].flag & AlgorithmFlags.MarkDegenerate) != 0;

                    // bad triangles should already have been removed by
                    // DegenPrologue(), but just in case check bIsDeg_a and bIsDeg_a are false
                    if (!bIsDeg_a && !bIsDeg_b)
                    {
                        bool bOrientA = (pTriInfos[t].flag & AlgorithmFlags.OrientPreserving) != 0;
                        bool bOrientB = (pTriInfos[t + 1].flag & AlgorithmFlags.OrientPreserving) != 0;
                        // if this happens the quad has extremely bad mapping!!
                        if (bOrientA != bOrientB)
                        {
                            //printf("found quad with bad mapping\n");
                            bool bChooseOrientFirstTri = false;
                            if ((pTriInfos[t + 1].flag & AlgorithmFlags.GroupWithAny) != 0)
                            {
                                bChooseOrientFirstTri = true;
                            }
                            else if (CalcTexArea(pContext, new ArrayPointerWrapper<int>(piTriListIn, t * 3 + 0)) >= CalcTexArea(pContext, new ArrayPointerWrapper<int>(piTriListIn, (t + 1) * 3 + 0)))
                            {
                                bChooseOrientFirstTri = true;
                            }

                            // force match
                            {
                                int t0 = bChooseOrientFirstTri ? t : (t + 1);
                                int t1 = bChooseOrientFirstTri ? (t + 1) : t;
                                pTriInfos[t1].flag &= (~AlgorithmFlags.OrientPreserving);    // clear first
                                pTriInfos[t1].flag |= (pTriInfos[t0].flag & AlgorithmFlags.OrientPreserving);   // copy bit
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

            Edge[] pEdges = new Edge[iNrTrianglesIn * 3];
            for (int j = 0; j < pEdges.Length; ++j)
            {
                pEdges[j] = new Edge();
            }

            BuildNeighborsFast(pTriInfos, pEdges, piTriListIn, iNrTrianglesIn);
        }

        private static void QuickSortEdges(Edge[] pSortBuffer, int iLeft, int iRight, int channel, uint uSeed)
        {
            uint t;
            int iL, iR, n, index, iMid;

            // early out
            Edge sTmp;
            int iElems = iRight - iLeft + 1;
            if (iElems < 2)
            {
                return;
            }
            else if (iElems == 2)
	        {
		        if (pSortBuffer[iLeft][channel] > pSortBuffer[iRight][channel])
		        {
			        sTmp = pSortBuffer[iLeft];
			        pSortBuffer[iLeft] = pSortBuffer[iRight];
			        pSortBuffer[iRight] = sTmp;
		        }

		        return;
	        }

            // Random
            t = uSeed & 31;
            t = (uSeed << (int)t) | (uSeed >> (32 - (int)t));
            uSeed = uSeed + t + 3;
            // Random end

            iL = iLeft;
            iR = iRight;
            n = (iR - iL) + 1;
            index = (int)(uSeed % n);

            iMid = pSortBuffer[index + iL][channel];

            do
            {
                while (pSortBuffer[iL][channel] < iMid)
                {
                    ++iL;
                }

                while (pSortBuffer[iR][channel] > iMid)
                {
                    --iR;
                }

                if (iL <= iR)
                {
                    sTmp = pSortBuffer[iL];
                    pSortBuffer[iL] = pSortBuffer[iR];
                    pSortBuffer[iR] = sTmp;
                    ++iL; --iR;
                }
            }
            while (iL <= iR);

            if (iLeft < iR)
            {
                QuickSortEdges(pSortBuffer, iLeft, iR, channel, uSeed);
            }

            if (iL < iRight)
            {
                QuickSortEdges(pSortBuffer, iL, iRight, channel, uSeed);
            }
        }

        private static void GetEdge(ref int i0_out, ref int i1_out, ref int edgenum_out, ArrayPointerWrapper<int>indices, int i0_in, int i1_in)
        {
	        edgenum_out = -1;
            // test if first index is on the edge
            if (indices[0] == i0_in || indices[0] == i1_in)
	        {
                // test if second index is on the edge
                if (indices[1] == i0_in || indices[1] == i1_in)
		        {
                    edgenum_out = 0;    // first edge
                    i0_out = indices[0];
                    i1_out = indices[1];
		        }
		        else
		        {
                    edgenum_out = 2;    // third edge
                    i0_out = indices[2];
                    i1_out = indices[0];
		        }
	        }

            else
            {
                // only second and third index is on the edge
                edgenum_out = 1; // second edge
                i0_out = indices[1];
                i1_out = indices[2];
            }
        }

        private static void BuildNeighborsFast(Triangle[] pTriInfos, Edge[] pEdges, int[] piTriListIn, int iNrTrianglesIn)
        {
            // build array of edges
            uint uSeed = 39871946;                // could replace with a random seed?
            int iEntries = 0, iCurStartIndex = -1, f = 0, i = 0;
            for (f = 0; f < iNrTrianglesIn; f++)
            {
                for (i = 0; i < 3; i++)
		        {
			        int i0 = piTriListIn[f * 3 + i];
                    int i1 = piTriListIn[f * 3 + (i < 2 ? (i + 1) : 0)];
                    pEdges[f * 3 + i].i0 = i0 < i1 ? i0 : i1;          // put minimum index in i0
                    pEdges[f * 3 + i].i1 = !(i0 < i1) ? i0 : i1;        // put maximum index in i1
                    pEdges[f * 3 + i].f = f;							// record face number
		        }
            }

            // sort over all edges by i0, this is the pricy one.
            QuickSortEdges(pEdges, 0, iNrTrianglesIn*3-1, 0, uSeed);    // sort channel 0 which is i0

            // sub sort over i1, should be fast.
            // could replace this with a 64 bit int sort over (i0,i1)
            // with i0 as msb in the quicksort call above.
            iEntries = iNrTrianglesIn*3;
	        iCurStartIndex = 0;
	        for (i=1; i<iEntries; i++)
	        {
		        if (pEdges[iCurStartIndex].i0 != pEdges[i].i0)
		        {
			        int iL = iCurStartIndex;
                    int iR = i - 1;
                    //const int iElems = i-iL;
                    iCurStartIndex = i;
			        QuickSortEdges(pEdges, iL, iR, 1, uSeed);   // sort channel 1 which is i1
                }
	        }

	        // sub sort over f, which should be fast.
	        // this step is to remain compliant with BuildNeighborsSlow() when
	        // more than 2 triangles use the same edge (such as a butterfly topology).
	        iCurStartIndex = 0;
            for (i = 1; i < iEntries; i++)
            {
                if (pEdges[iCurStartIndex].i0 != pEdges[i].i0 || pEdges[iCurStartIndex].i1 != pEdges[i].i1)
                {
                    int iL = iCurStartIndex;
                    int iR = i - 1;
                    //const int iElems = i-iL;
                    iCurStartIndex = i;
                    QuickSortEdges(pEdges, iL, iR, 2, uSeed);   // sort channel 2 which is f
                }
            }

            // pair up, adjacent triangles
            for (i = 0; i < iEntries; i++)
            {
                int i0 = pEdges[i].i0;
                int i1 = pEdges[i].i1;
                f = pEdges[i].f;
                bool bUnassigned_A;

                int i0_A = 0, i1_A = 0;
                int edgenum_A = 0, edgenum_B = 0;   // 0,1 or 2
                GetEdge(ref i0_A, ref i1_A, ref edgenum_A, new ArrayPointerWrapper<int>(piTriListIn, f * 3), i0, i1); // resolve index ordering and edge_num
                bUnassigned_A = pTriInfos[f].faceNeighbours[edgenum_A] == -1;

                if (bUnassigned_A)
                {
                    // get true index ordering
                    int j = i + 1, t;
                    bool bNotFound = true;
                    while (j < iEntries && i0 == pEdges[j].i0 && i1 == pEdges[j].i1 && bNotFound)
                    {
                        bool bUnassigned_B;
                        int i0_B = 0, i1_B = 0;
                        t = pEdges[j].f;
                        // flip i0_B and i1_B
                        GetEdge(ref i1_B, ref i0_B, ref edgenum_B, new ArrayPointerWrapper<int>(piTriListIn, t * 3), pEdges[j].i0, pEdges[j].i1); // resolve index ordering and edge_num
                                                                                                            //assert(!(i0_A==i1_B && i1_A==i0_B));
                        bUnassigned_B = pTriInfos[t].faceNeighbours[edgenum_B] == -1;
                        if (i0_A == i0_B && i1_A == i1_B && bUnassigned_B)
                        {
                            bNotFound = false;
                        }
                        else
                        {
                            ++j;
                        }
                    }

                    if (!bNotFound)
                    {
                        int t1 = pEdges[j].f;
                        pTriInfos[f].faceNeighbours[edgenum_A] = t1;
                        //assert(pTriInfos[t].FaceNeighbors[edgenum_B]==-1);
                        pTriInfos[t1].faceNeighbours[edgenum_B] = f;
                    }
                }
            }
        }

        private static void AddTriToGroup(Group pGroup, int iTriIndex)
        {

            pGroup.faceIndices[pGroup.numFaces] = iTriIndex;
	        ++pGroup.numFaces;
        }

        private static bool AssignRecur(int[] piTriListIn, Triangle[] psTriInfos, int iMyTriIndex, Group pGroup)
        {
	        Triangle pMyTriInfo = psTriInfos[iMyTriIndex];

            // track down vertex
            int iVertRep = pGroup.vertexRepresentitive;
            ArrayPointerWrapper<int> pVerts = new ArrayPointerWrapper<int>(piTriListIn, 3 * iMyTriIndex + 0);
            int i = -1;
            if (pVerts[0] == iVertRep)
            {
                i = 0;
            }
            else if (pVerts[1] == iVertRep)
            {
                i = 1;
            }
            else if (pVerts[2] == iVertRep)
            {
                i = 2;
            }

            // early out
            if (pMyTriInfo.assignedGroups[i] == pGroup)
            {
                return true;
            }
            else if (pMyTriInfo.assignedGroups[i] != null)
            {
                return false;
            }

            if ((pMyTriInfo.flag & AlgorithmFlags.GroupWithAny) != 0)
	        {
		        // first to group with a group-with-anything triangle
		        // determines it's orientation.
		        // This is the only existing order dependency in the code!!
		        if (pMyTriInfo.assignedGroups[0] == null &&
			        pMyTriInfo.assignedGroups[1] == null &&
			        pMyTriInfo.assignedGroups[2] == null)
		        {
			        pMyTriInfo.flag &= (~AlgorithmFlags.OrientPreserving);
                    pMyTriInfo.flag |= (pGroup.orientPreservering? AlgorithmFlags.OrientPreserving : 0);
                }
            }

            bool bOrient = (pMyTriInfo.flag & AlgorithmFlags.OrientPreserving) != 0;
            if (bOrient != pGroup.orientPreservering)
            {
                return false;
            }

            AddTriToGroup(pGroup, iMyTriIndex);
            pMyTriInfo.assignedGroups[i] = pGroup;

            int neigh_indexL = pMyTriInfo.faceNeighbours[i];
            int neigh_indexR = pMyTriInfo.faceNeighbours[i > 0 ? (i - 1) : 2];
            if (neigh_indexL >= 0)
            {
                AssignRecur(piTriListIn, psTriInfos, neigh_indexL, pGroup);
            }

            if (neigh_indexR >= 0)
            {
                AssignRecur(piTriListIn, psTriInfos, neigh_indexR, pGroup);
            }

            return true;
        }

        private static int Build4RuleGroups(Triangle[] pTriInfos, Group[] pGroups, int[] piGroupTrianglesBuffer, int[] piTriListIn, int iNrTrianglesIn)
        {

            int iNrMaxGroups = iNrTrianglesIn * 3;
            int iNrActiveGroups = 0;
            int iOffset = 0, f = 0, i = 0;
            for (f = 0; f < iNrTrianglesIn; f++)
	        {
                for (i = 0; i < 3; i++)
		        {
			        // if not assigned to a group
			        if ((pTriInfos[f].flag & AlgorithmFlags.GroupWithAny)==0 && pTriInfos[f].assignedGroups[i] == null)
			        {
				        bool bOrPre;
                        int neigh_indexL, neigh_indexR;
                        int vert_index = piTriListIn[f * 3 + i];
                        pTriInfos[f].assignedGroups[i] = pGroups[iNrActiveGroups];
				        pTriInfos[f].assignedGroups[i].vertexRepresentitive = vert_index;
                        pTriInfos[f].assignedGroups[i].orientPreservering = (pTriInfos[f].flag & AlgorithmFlags.OrientPreserving) != 0;
				        pTriInfos[f].assignedGroups[i].numFaces = 0;
				        pTriInfos[f].assignedGroups[i].faceIndices = new ArrayPointerWrapper<int>(piGroupTrianglesBuffer, iOffset);
				        ++iNrActiveGroups;

				        AddTriToGroup(pTriInfos[f].assignedGroups[i], f);
                        bOrPre = (pTriInfos[f].flag & AlgorithmFlags.OrientPreserving) != 0;
				        neigh_indexL = pTriInfos[f].faceNeighbours[i];
				        neigh_indexR = pTriInfos[f].faceNeighbours[i > 0 ? (i - 1) : 2];
				        if (neigh_indexL>=0) // neighbor
				        {
					        bool bAnswer = AssignRecur(piTriListIn, pTriInfos, neigh_indexL, pTriInfos[f].assignedGroups[i]);
                            bool bOrPre2 = (pTriInfos[neigh_indexL].flag & AlgorithmFlags.OrientPreserving) != 0;
                            bool bDiff = bOrPre != bOrPre2;
				        }

				        if (neigh_indexR>=0) // neighbor
				        {
					        bool bAnswer = AssignRecur(piTriListIn, pTriInfos, neigh_indexR, pTriInfos[f].assignedGroups[i]);
                            bool bOrPre2 = (pTriInfos[neigh_indexR].flag & AlgorithmFlags.OrientPreserving) != 0;
                            bool bDiff = bOrPre != bOrPre2;
				        }

                        // update offset
                        iOffset += pTriInfos[f].assignedGroups[i].numFaces;
                        // since the groups are disjoint a triangle can never
                        // belong to more than 3 groups. Subsequently something
                        // is completely screwed if this assertion ever hits.
			        }
		        }
	        }

	        return iNrActiveGroups;
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
                get => i == 0 ? this.i0 : i == 1 ? this.i1 : this.f;
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

        private static void QuickSort(int[] pSortBuffer, int iLeft, int iRight, uint uSeed)
        {
            int iL, iR, n, index, iMid, iTmp;

            // Random
            uint t = uSeed & 31;
            t = (uSeed << (int)t) | (uSeed >> (32 - (int)t));
            uSeed = uSeed + t + 3;
            // Random end

            iL = iLeft; iR = iRight;
            n = (iR - iL) + 1;
            index = (int)(uSeed % n);
            iMid = pSortBuffer[index + iL];
            do
            {
                while (pSortBuffer[iL] < iMid)
                {
                    ++iL;
                }

                while (pSortBuffer[iR] > iMid)
                {
                    --iR;
                }

                if (iL <= iR)
                {
                    iTmp = pSortBuffer[iL];
                    pSortBuffer[iL] = pSortBuffer[iR];
                    pSortBuffer[iR] = iTmp;
                    ++iL; --iR;
                }
            }
            while (iL <= iR);

            if (iLeft < iR)
            {
                QuickSort(pSortBuffer, iLeft, iR, uSeed);
            }

            if (iL < iRight)
            {
                QuickSort(pSortBuffer, iL, iRight, uSeed);
            }
        }

        private static bool CompareSubGroups(SubGroup pg1, SubGroup pg2)
        {
            bool bStillSame = true;
            int i = 0;
            if (pg1.nrFaces != pg2.nrFaces)
            {
                return false;
            }

            while (i < pg1.nrFaces && bStillSame)
	        {
                bStillSame = pg1.triMembers[i] == pg2.triMembers[i];
                if (bStillSame)
                {
                    ++i;
                }
            }

	        return bStillSame;
        }

        private static TSpace EvalTspace(int[] face_indices, int iFaces, int[] piTriListIn, Triangle[] pTriInfos,
                          Context pContext, int iVertexRepresentitive)
        {

            TSpace res = new TSpace();
            float fAngleSum = 0;
            int face = 0;
            res.os = Vector3.Zero;
            res.ot = Vector3.Zero;
            res.magT = res.magS = 0;
	        for (face=0; face<iFaces; face++)
	        {
		        int f = face_indices[face];

                // only valid triangles get to add their contribution
                if ((pTriInfos[f].flag & AlgorithmFlags.GroupWithAny) == 0)
		        {
			        Vector3 n = Vector3.Zero, vOs = Vector3.Zero, vOt = Vector3.Zero, p0 = Vector3.Zero, p1 = Vector3.Zero, p2 = Vector3.Zero, v1 = Vector3.Zero, v2 = Vector3.Zero;
                    float fCos, fAngle, fMagS, fMagT;
                    int i = -1, index = -1, i0 = -1, i1 = -1, i2 = -1;
                    if (piTriListIn[(3 * f) + 0] == iVertexRepresentitive)
                    {
                        i = 0;
                    }
                    else if (piTriListIn[3 * f + 1] == iVertexRepresentitive)
                    {
                        i = 1;
                    }
                    else if (piTriListIn[3 * f + 2] == iVertexRepresentitive)
                    {
                        i = 2;
                    }


                    // project
                    index = piTriListIn[3 * f + i];
			        n = pContext.normals[index];
                    vOs = Vector3.Subtract(pTriInfos[f].os, Vector3.Multiply(Vector3.Dot(n, pTriInfos[f].os), n));
			        vOt = Vector3.Subtract(pTriInfos[f].ot, Vector3.Multiply(Vector3.Dot(n, pTriInfos[f].ot), n));
                    if (NotZero(vOs))
                    {
                        vOs = Vector3.Normalize(vOs);
                    }

                    if (NotZero(vOt))
                    {
                        vOt = Vector3.Normalize(vOt);
                    }

                    i2 = piTriListIn[3 * f + (i < 2 ? (i + 1) : 0)];
			        i1 = piTriListIn[3 * f + i];
			        i0 = piTriListIn[3 * f + (i > 0 ? (i - 1) : 2)];

			        p0 = pContext.positions[i0];
                    p1 = pContext.positions[i1];
                    p2 = pContext.positions[i2];
                    v1 = Vector3.Subtract(p0, p1);
                    v2 = Vector3.Subtract(p2, p1);

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
                    fMagS = pTriInfos[f].magS;
			        fMagT = pTriInfos[f].magT;

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

        private static bool GenerateTSpaces(TSpace[] psTspace, Triangle[] pTriInfos, Group[] pGroups,
                             int iNrActiveGroups, int[] piTriListIn, float fThresCos,
                             Context pContext)
        {

            TSpace[] pSubGroupTspace = null;
            SubGroup[] pUniSubGroups = null;
            int[] pTmpMembers = null;
            int iMaxNrFaces = 0, iUniqueTspaces = 0, g = 0, i = 0;
            for (g = 0; g < iNrActiveGroups; g++)
            {
                if (iMaxNrFaces < pGroups[g].numFaces)
                {
                    iMaxNrFaces = pGroups[g].numFaces;
                }
            }

            if (iMaxNrFaces == 0)
            {
                return true;
            }

            // make initial allocations
            pSubGroupTspace = new TSpace[iMaxNrFaces];
            for (int j = 0; j < iMaxNrFaces; ++j)
            {
                pSubGroupTspace[j] = new TSpace();
            }

            pUniSubGroups = new SubGroup[iMaxNrFaces];
            for (int j = 0; j < iMaxNrFaces; ++j)
            {
                pUniSubGroups[j] = new SubGroup();
            }

            pTmpMembers = new int[iMaxNrFaces];
            iUniqueTspaces = 0;
            for (g = 0; g < iNrActiveGroups; g++)
	        {
		        Group pGroup = pGroups[g];
                int iUniqueSubGroups = 0, s = 0;
                for (i = 0; i < pGroup.numFaces; i++)  // triangles
                {
			        int f = pGroup.faceIndices[i];  // triangle number
                    int index = -1, iVertIndex = -1, iOF_1 = -1, iMembers = 0, j = 0, l = 0;
                    SubGroup tmp_group = new SubGroup();
                    bool bFound;
                    Vector3 n, vOs, vOt;
                    if (pTriInfos[f].assignedGroups[0] == pGroup)
                    {
                        index = 0;
                    }
                    else if (pTriInfos[f].assignedGroups[1] == pGroup)
                    {
                        index = 1;
                    }
                    else if (pTriInfos[f].assignedGroups[2] == pGroup)
                    {
                        index = 2;
                    }

                    iVertIndex = piTriListIn[f * 3 + index];

                    // is normalized already
                    n = pContext.normals[iVertIndex];

                    // project
                    vOs = Vector3.Subtract(pTriInfos[f].os, Vector3.Multiply(Vector3.Dot(n, pTriInfos[f].os), n));
			        vOt = Vector3.Subtract(pTriInfos[f].ot, Vector3.Multiply(Vector3.Dot(n, pTriInfos[f].ot), n));
                    if (NotZero(vOs))
                    {
                        vOs = Vector3.Normalize(vOs);
                    }

                    if (NotZero(vOt))
                    {
                        vOt = Vector3.Normalize(vOt);
                    }

                    // original face number
                    iOF_1 = pTriInfos[f].orgFaceNumber;
			        iMembers = 0;
                    for (j = 0; j < pGroup.numFaces; j++)
			        {
				        int t = pGroup.faceIndices[j];  // triangle number
                        int iOF_2 = pTriInfos[t].orgFaceNumber;

                        // project
                        Vector3 vOs2 = Vector3.Subtract(pTriInfos[t].os, Vector3.Multiply(Vector3.Dot(n, pTriInfos[t].os), n));
                        Vector3 vOt2 = Vector3.Subtract(pTriInfos[t].ot, Vector3.Multiply(Vector3.Dot(n, pTriInfos[t].ot), n));
                        if (NotZero(vOs2))
                        {
                            vOs2 = Vector3.Normalize(vOs2);
                        }

                        if (NotZero(vOt2))
                        {
                            vOt2 = Vector3.Normalize(vOt2);
                        }

                        bool bAny = ((pTriInfos[f].flag | pTriInfos[t].flag) & AlgorithmFlags.GroupWithAny) != 0;
                        // make sure triangles which belong to the same quad are joined.
                        bool bSameOrgFace = iOF_1 == iOF_2;
                        float fCosS = Vector3.Dot(vOs, vOs2);
                        float fCosT = Vector3.Dot(vOt, vOt2);
                        if (bAny || bSameOrgFace || (fCosS > fThresCos && fCosT > fThresCos))
                        {
                            pTmpMembers[iMembers++] = t;
                        }
			        }

			        // sort pTmpMembers
			        tmp_group.nrFaces = iMembers;
                    tmp_group.triMembers = pTmpMembers;
                    if (iMembers > 1)
                    {
                        uint uSeed = 39871946;    // could replace with a random seed?
                        QuickSort(pTmpMembers, 0, iMembers - 1, uSeed);
                    }

                    // look for an existing match
                    bFound = false;
                    l = 0;
                    while (l < iUniqueSubGroups && !bFound)
                    {
                        bFound = CompareSubGroups(tmp_group, pUniSubGroups[l]);
                        if (!bFound)
                        {
                            ++l;
                        }
                    }

                    // assign tangent space index
                    //piTempTangIndices[f*3+index] = iUniqueTspaces+l;

                    // if no match was found we allocate a new subgroup
                    if (!bFound)
                    {
                        // insert new subgroup
                        int[] pIndices = new int[iMembers];
                        pUniSubGroups[iUniqueSubGroups].nrFaces = iMembers;
                        pUniSubGroups[iUniqueSubGroups].triMembers = pIndices;
                        Array.Copy(tmp_group.triMembers, pIndices, iMembers);
                        pSubGroupTspace[iUniqueSubGroups] =
                        EvalTspace(tmp_group.triMembers, iMembers, piTriListIn, pTriInfos, pContext, pGroup.vertexRepresentitive);
                        ++iUniqueSubGroups;
                    }

                    // output tspace
                    int iOffs = pTriInfos[f].tSpacesOffset;
                    int iVert = pTriInfos[f].vertexNum[index];
                    TSpace pTS_out = psTspace[iOffs + iVert];
                    if (pTS_out.counter == 1)
                    {
                        pTS_out = AvgTSpace(pTS_out, pSubGroupTspace[l]);
                        pTS_out.counter = 2;  // update counter
                        pTS_out.orient = pGroup.orientPreservering;
                    }
                    else
                    {
                        pTS_out = pSubGroupTspace[l];
                        pTS_out.counter = 1;  // update counter
                        pTS_out.orient = pGroup.orientPreservering;
                    }
		        }

		        // clean up and offset iUniqueTspaces
                iUniqueTspaces += iUniqueSubGroups;
	        }

            return true;
        }

        private struct ArrayPointerWrapper<T>
        {
            private T[] arr;
            private int offset;

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
            public int nrFaces;
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
