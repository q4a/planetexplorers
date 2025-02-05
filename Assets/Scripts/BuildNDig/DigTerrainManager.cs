using UnityEngine;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using SkillAsset;
using ItemAsset;
using NaturalResAsset;
using CustomData;
using SkillSystem;

//TODO : VFVoxelTerrain should be unique.
//TODO : code refact
public class DigTerrainManager
{
	static Dictionary<IntVector3, float> BlockVolumes = new Dictionary<IntVector3, float>();
	static IntVector3[] s_vegOfsPos = new IntVector3[]{
		new IntVector3(0,0,0),
		new IntVector3(-1,0,0),
		new IntVector3(+1,0,0),
		new IntVector3(0,0,-1),
		new IntVector3(0,0,+1),
	};

	// Call back --- add By WuYiqiu
	public delegate void DigTerrainDel (IntVector3 pos);
	static public event DigTerrainDel onDigTerrain;

	public  delegate void DirtyVoxelEvent(Vector3 pos, byte terrainType);
	static public event DirtyVoxelEvent onDirtyVoxel;

	//static readonly float VoxelScaleMin = 0.02352f;
	//static readonly float VoxelScaleMax = 0.21897f;

	static readonly float VoxelDamageMin = 0f;
	static readonly float VoxelDamageMax =  1275f;
	static readonly float VoxelDamageDT =  VoxelDamageMax - VoxelDamageMin;
	static readonly float ReduceF = 0.33f;
	static readonly float MinDamage = 2.5f;
	public static readonly int   DonotChangeVoxelNotice = 8000175;

	static List<DragItemMousePickPlant> mDigPlant = new List<DragItemMousePickPlant> ();
    static List<Tuple<Vector3, int>> trees = new List<Tuple<Vector3, int>>();
    static List<Vector3> grasses = new List<Vector3>();

    static void DeleteVegetation(IntVector3 treePos)
    {
        Vector3 pos;
        for (int i = 0; i < s_vegOfsPos.Length; i++)
        {
            pos = treePos + s_vegOfsPos[i];
            DeleteVegetation(pos);
        }
    }

    static void DeleteVegetation(Vector3 pos)
    {
        if (null != LSubTerrainMgr.Instance) LSubTerrainMgr.DeleteTreesAtPos(pos);
        else if (null != RSubTerrainMgr.Instance) RSubTerrainMgr.DeleteTreesAtPos(pos);

        PeGrassSystem.DeleteAtPos(pos);
    }

    static void DigBlocks(IntVector3 blockUnitPos, float durDec, ref List<B45Block> removeList)
	{
		IntVector3 index = blockUnitPos;		
		B45Block block = Block45Man.self.DataSource.SafeRead(index.x, index.y, index.z);
		if(!BlockVolumes.ContainsKey(index))
			BlockVolumes[index] = 255f;
		
		if(block.blockType != 0)
		{
			int ItemID = PEBuildingMan.GetBlockItemProtoID(block.materialType);
			ItemProto item = ItemProto.GetItemData(ItemID);
			if(item == null){
				string strErr = "ItemProto not found for material type "+block.materialType;
#if UNITY_EDITOR
				throw new Exception(strErr);
#else
                Debug.LogError(strErr);
				return;
#endif
            }
            NaturalRes resData = NaturalRes.GetTerrainResData(item.setUp);
			if(resData == null){
				string strErr = "NaturalRes not found for item "+item.id;
#if UNITY_EDITOR
				throw new Exception(strErr);
#else
                Debug.LogError(strErr);
                return;
#endif
            }
            float digPower = durDec * resData.m_duration;
			BlockVolumes[index] -= digPower;
			if(BlockVolumes[index] <= 0)
			{
				Block45Man.self.DataSource.SafeWrite(new B45Block(0,0), index.x, index.y, index.z, 0);
				BlockVolumes.Remove(index);
				//EffectManager.Instance.Instantiate(47,worldPos,Quaternion.identity);
			}
		}
	}

	public static void ClearBlockInfo()
	{
		BlockVolumes.Clear();
	}

	public static void ClearColonyBlockInfo(CSAssembly assembly)
	{
		if(null == assembly) return;

		List<IntVector3> removeList = new List<IntVector3>();

		foreach(IntVector3 pos in BlockVolumes.Keys)
			if(assembly.InRange(pos.ToVector3()))
				removeList.Add(pos);

		for(int i = 0; i < removeList.Count; ++i)
			BlockVolumes.Remove(removeList[i]);

		removeList.Clear();
	}

	public static int DigBlock45 (IntVector3 intPos, float durDec, float radius, float height, ref List<B45Block> removeList, bool square = true)
	{
		int count = 0;
		bool finish = true;
		int cx = intPos.x * Block45Constants._scaleInverted;
		int cy = intPos.y * Block45Constants._scaleInverted;
		int cz = intPos.z * Block45Constants._scaleInverted;
		int rBlocks = (int)(radius * Block45Constants._scaleInverted);
		int hBlocks = (int)(height * Block45Constants._scaleInverted);
		float maxSqr = radius * radius;
		int maxSqrBlocks = (int)(maxSqr*Block45Constants._scaleInverted*Block45Constants._scaleInverted);
		for (int x = -rBlocks; x <= rBlocks; ++x)
		{
			for (int z = -rBlocks; z <= rBlocks; ++z)
			{
				for(int y = -hBlocks; y <= hBlocks; ++y)
				{
					int sqr = x*x + y*y + z*z;
					if(!square && sqr > maxSqrBlocks)
						continue;

					IntVector3 idx = new IntVector3(cx+x,
					                                cy+y,
					                                cz+z);
					float power = durDec;// * (1f - Mathf.Clamp01((float)sqr / maxSqrBlocks) * 0.25f);
					DigBlocks(idx, power, ref removeList);
					// Call back
					//if (onDigTerrain != null)
					//	onDigTerrain(idx);
				}
			}
		}
		for (float x = -radius; x <= radius; ++x) {
			for (float z = -radius; z <= radius; ++z) {
				for(float y = -height; y <= height; ++y){
					if(!square && x*x + y*y + z*z > maxSqr)
						continue;

					IntVector3 idx = new IntVector3(Mathf.FloorToInt(intPos.x+x),
					                                Mathf.FloorToInt(intPos.y+y),
					                                Mathf.FloorToInt(intPos.z+z));
					DeleteVegetation(idx);
				}
			}
		}
		if(null != LSubTerrainMgr.Instance)
			LSubTerrainMgr.RefreshAllLayerTerrains();
		else if(null != RSubTerrainMgr.Instance)
			RSubTerrainMgr.RefreshAllLayerTerrains();
		
		//		GrassMgr.RefreshDirtyChunks();
		
		if(finish)
			count += 100;
		return count;
	}

	//return 0 Novoxel, 1 dig complete , 2 dig notcomplete
	static void DigVoxels(IntVector3 intPos, float durDec, ref List<VFVoxel> removeList)
	{
		Vector3 buildPos = Vector3.zero;
		buildPos.x = intPos.x;
		buildPos.y = intPos.y;
		buildPos.z = intPos.z;
		VFVoxel getVoxel = VFVoxelTerrain.self.Voxels.SafeRead(intPos.x, intPos.y, intPos.z);
		if (getVoxel.Volume == 0)
			return;

		float digPower = durDec;
		NaturalRes resData = NaturalRes.GetTerrainResData(getVoxel.Type);
		if(null != resData)
		{
			float damageP = (digPower - VoxelDamageMin) / VoxelDamageDT;
			float hpP = 37.14689f * resData.m_duration * resData.m_duration - 14.12429f * resData.m_duration + 1.39f;
			if(hpP - damageP > ReduceF)
				digPower *= 1f - Mathf.Clamp01((hpP - damageP - ReduceF) / ReduceF);
			digPower *= resData.m_duration;
			if(digPower < MinDamage)
				digPower = MinDamage;
		}
		else
			Debug.LogWarning("VoxelType[" + getVoxel.Type + "] does't have NaturalRes data.");

		if (digPower >= 255)
			getVoxel.Volume = 0;
		else if (getVoxel.Volume > digPower)
			getVoxel.Volume -= (byte)digPower;
		else
			getVoxel.Volume = 0;
		if (getVoxel.Volume <= 127)
		{
			getVoxel.Volume = 0;
			VFVoxelTerrain.self.AlterVoxelInBuild(buildPos, new VFVoxel(0));
			removeList.Add(getVoxel);
			return;
		}
		else
		{
			VFVoxelTerrain.self.AlterVoxelInBuild(buildPos, getVoxel);
			return;
		}
	}
	public static int DigTerrain (IntVector3 intPos, float durDec,float radius, float height, ref List<VFVoxel> removeList, bool square = true)
	{
        //LogManager.Debug("DigTerrain:" + intPos);
		int count = 0;
		bool finish = true;
		                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   
		for (float _x = -radius; _x <= radius; ++_x)
		{
			for (float _z = -radius; _z <= radius; ++_z)
			{
				for(float _y = 0; _y <= height; ++_y)
				{

					float sqrMagnitude = _x * _x + _y * _y + _z * _z;

					if(!square && sqrMagnitude > radius * radius)
						continue;

					IntVector3 _idx = new IntVector3 (intPos.x + _x, intPos.y + _y, intPos.z + _z);
					DeleteVegetation(_idx);					
					DigVoxels(_idx, durDec, ref removeList);
					
					// Call back
					if (onDigTerrain != null)
						onDigTerrain(_idx);
				}
			}
		}

		DigPlant (intPos.ToVector3(), radius);
		
		if(null != LSubTerrainMgr.Instance)
			LSubTerrainMgr.RefreshAllLayerTerrains();
		else if(null != RSubTerrainMgr.Instance)
			RSubTerrainMgr.RefreshAllLayerTerrains();


		
//		GrassMgr.RefreshDirtyChunks();
		
		if(finish)
			count += 100;
		return count;
	}

	static void DigPlant(Vector3 position, float radius)
	{
		Collider[] cols = Physics.OverlapSphere (position, radius);
		DragItemMousePickPlant plant;
		for (int i = 0; i < cols.Length; ++i) 
		{
			if(cols[i].tag == "Plant")
			{
				plant = cols[i].GetComponentInParent<DragItemMousePickPlant>();
				if(null != plant && !mDigPlant.Contains(plant))
				{
					plant.OnClear();
					mDigPlant.Add(plant);
				}
			}
		}
		mDigPlant.Clear ();
	}

	public static void ChangeTerrain(IntVector3 intPos, float radius, byte targetType,SkEntity dig = null)
	{
        if (GameConfig.IsMultiMode && dig != null && dig.IsController())
		{
			List<VFVoxel> changedVoxel = new List<VFVoxel>();
			grasses.Clear();

            for (float _x = -radius; _x <= radius; _x++)
            {
                for (float _z = -radius; _z <= radius; _z++)
                {
                    for (float _y = -radius; _y <= radius; _y++)
                    {
                        IntVector3 pos = new IntVector3(intPos.x + _x, intPos.y + _y, intPos.z + _z);
                        VFVoxel voxel = VFVoxelTerrain.self.Voxels.SafeRead(pos.x, pos.y, pos.z);
						changedVoxel.Add(voxel);

						Vector3 outPos;
						if (PeGrassSystem.DeleteAtPos(pos, out outPos))
							grasses.Add(outPos);
                    }
                }
            }

            if (changedVoxel.Count != 0)
            {
                byte[] data = PETools.Serialize.Export(w =>
                {
                    w.Write(changedVoxel.Count);
                    foreach (var voxel in changedVoxel)
                        BufferHelper.Serialize(w, voxel);
                });

                dig._net.RPCServer(EPacketType.PT_InGame_SKChangeTerrain, intPos, radius, targetType, data);
            }

            if (grasses.Count != 0)
            {
                byte[] grassData = PETools.Serialize.Export(w =>
                {
                    BufferHelper.Serialize(w, grasses.Count);
                    foreach (var pos in grasses)
                        BufferHelper.Serialize(w, pos);
                });

                dig._net.RPCServer(EPacketType.PT_InGame_ClearGrass, grassData);
            }
        }
        else
        {
			Dictionary<IntVector3, VFVoxel> changedVoxel = new Dictionary<IntVector3, VFVoxel>();
			for (float _x = -radius; _x <= radius; _x++)
            {
                for (float _z = -radius; _z <= radius; _z++)
                {
                    for (float _y = -radius; _y <= radius; _y++)
                    {
                        IntVector3 pos = new IntVector3(intPos.x + _x, intPos.y + _y, intPos.z + _z);
                        VFVoxel voxel = VFVoxelTerrain.self.Voxels.SafeRead(pos.x, pos.y, pos.z);
                        if (voxel.Type != 0 && voxel.Type != targetType)
                        {
                            if (voxel.Volume > 128)
                                changedVoxel[pos] = new VFVoxel((byte)((voxel.Volume < (byte)235) ? (voxel.Volume + (byte)20) : (byte)255), targetType);
                            else
                                changedVoxel[pos] = new VFVoxel(voxel.Volume, targetType);

                            VFVoxelTerrain.self.AlterVoxelInBuild(pos, changedVoxel[pos]);
                        }

                        PeGrassSystem.DeleteAtPos(pos);
                        DirtyTerrain(pos, voxel, targetType);
                    }
                }
            }

			if (0 == changedVoxel.Count)
				new PeTipMsg(PELocalization.GetString(DonotChangeVoxelNotice), "", PeTipMsg.EMsgLevel.Norm);
		}
	}

	public static void ChangeTerrainNetReturn(IntVector3 intPos, float radius, byte targetType, byte[] data)
	{
		PETools.Serialize.Import(data, r =>
		{
			for (float _x = -radius; _x <= radius; _x++)
			{
				for (float _z = -radius; _z <= radius; _z++)
				{
					for (float _y = -radius; _y <= radius; _y++)
					{
						IntVector3 pos = new IntVector3(intPos.x + _x, intPos.y + _y, intPos.z + _z);
						VFVoxel voxel = VFVoxelTerrain.self.Voxels.SafeRead(pos.x, pos.y, pos.z);
						if (voxel.Type != 0 && voxel.Type != targetType)
						{
							if (voxel.Volume > 128)
								voxel = new VFVoxel((byte)((voxel.Volume < (byte)235) ? (voxel.Volume + (byte)20) : (byte)255), targetType);
							else
								voxel = new VFVoxel(voxel.Volume, targetType);

							ApplyBSDataFromNet(0, pos, new BSVoxel(voxel));
						}

						DirtyTerrain(pos, voxel, targetType);
					}
				}
			}
		});
	}

	public static Dictionary<int, int> GetResouce(List<VFVoxel> removeList, float bouns,bool bGetSpItems = false)
	{
		NaturalRes resData;
		Dictionary<int, int> voxelTypeNum = new Dictionary<int, int>();
		Dictionary<int, int> returnItems = new Dictionary<int, int>();

		if(removeList.Count == 0)
			return returnItems;

		foreach(VFVoxel voxel in removeList)
		{
			if(voxelTypeNum.ContainsKey(voxel.Type))
				voxelTypeNum[voxel.Type]++;
			else
				voxelTypeNum[voxel.Type] = 1;
		}
		
		foreach(int voxelType in voxelTypeNum.Keys)
		{
			if ((resData = NaturalRes.GetTerrainResData(voxelType)) != null && resData.m_itemsGot.Count > 0)
			{
				float resGet =0;
				if(resData.mFixedNum > 0)
					resGet = resData.mFixedNum;
				else
					resGet = (bouns + resData.mSelfGetNum);
				if(resGet < 0)
					resGet = 0;
				resGet *= voxelTypeNum[voxelType];
				resGet = (int)resGet + (UnityEngine.Random.value < (resGet - (int)resGet) ? 1 : 0);

				for(int i = 0; i < resGet; i++)
				{
					int randomValue = UnityEngine.Random.Range(0, 100);
					for (int j = 0; j < resData.m_itemsGot.Count; j++)
					{
						if (randomValue < resData.m_itemsGot[j].m_probablity)
						{
							if(returnItems.ContainsKey(resData.m_itemsGot[j].m_id))
								returnItems[resData.m_itemsGot[j].m_id]++;
							else
								returnItems[resData.m_itemsGot[j].m_id] = 1;
							break;
						}
					}
				}
				
				if(resData.m_extraGot.extraPercent > 0 && UnityEngine.Random.value < resGet * resData.m_extraGot.extraPercent)
				{
					resGet *= resData.m_extraGot.extraPercent;
					int rand;
					for(int i = 0; i < resGet; i++)
					{
						rand = UnityEngine.Random.Range(0, 100);
						for(int j = 0; j < resData.m_extraGot.m_extraGot.Count; j++)
						{
							if(rand < resData.m_extraGot.m_extraGot[j].m_probablity)
							{
								if(returnItems.ContainsKey(resData.m_extraGot.m_extraGot[j].m_id))
									returnItems[resData.m_extraGot.m_extraGot[j].m_id]++;
								else
									returnItems[resData.m_extraGot.m_extraGot[j].m_id] = 1;
								break;
							}
						}
					}
				}
				if(bGetSpItems)
				{
					if(resData.mFixedNum > 0)
						resGet = resData.mFixedNum;
					else
						resGet = (bouns + resData.mSelfGetNum);
					if(resGet < 0)
						resGet = 0;
					resGet *= voxelTypeNum[voxelType];
					resGet = (int)resGet + (UnityEngine.Random.value < (resGet - (int)resGet) ? 1 : 0);
					if(resData.m_extraSpGot.extraPercent > 0 && UnityEngine.Random.value < resGet * resData.m_extraSpGot.extraPercent)
					{
						resGet *= resData.m_extraSpGot.extraPercent;
						int rand;
						for(int i = 0; i < resGet; i++)
						{
							rand = UnityEngine.Random.Range(0, 100);
							for(int j = 0; j < resData.m_extraSpGot.m_extraGot.Count; j++)
							{
								if(rand < resData.m_extraSpGot.m_extraGot[j].m_probablity)
								{
									if(returnItems.ContainsKey(resData.m_extraSpGot.m_extraGot[j].m_id))
										returnItems[resData.m_extraSpGot.m_extraGot[j].m_id]++;
									else
										returnItems[resData.m_extraSpGot.m_extraGot[j].m_id] = 1;
									break;
								}
							}
						}
					}
				}
			}
		}

		return returnItems;
	}

	public static void DestroyTerrainInRange(int type, Vector3 pos, float power, float radius)
	{
		bool needRefresh = false;
		if(type == 2)
		{
			IntVector3 basePos = new IntVector3(pos - radius * Vector3.one);
			for(float i = 0; i < 2 * radius; i++)
			{
				for(float j = 0; j < 2 * radius; j++)
				{
					for(float k = 0; k < 2 * radius; k++)
					{
						IntVector3 digPos = new IntVector3(basePos.x + i, basePos.y + j, basePos.z + k);
						if(Vector3.Distance(digPos, pos) <= radius)
						{
							VFVoxel getVoxel = VFVoxelTerrain.self.Voxels.SafeRead(digPos.x, digPos.y, digPos.z);
							if(null != LSubTerrainMgr.Instance)
							{
								LSubTerrainMgr.DeleteTreesAtPos(digPos);
								if(!needRefresh)
									needRefresh = (LSubTerrainMgr.Picking(digPos,true).Count > 0);
							}
							else if(null != RSubTerrainMgr.Instance)
							{
								RSubTerrainMgr.DeleteTreesAtPos(digPos);
								if(!needRefresh)
									needRefresh = (RSubTerrainMgr.Picking(digPos,true).Count > 0);
							}
							PeGrassSystem.DeleteAtPos(pos);
							if(getVoxel.Volume > 0)
							{
								NaturalRes resData = NaturalRes.GetTerrainResData(getVoxel.Type);
								float digPower = power * resData.m_duration * (1f - Mathf.Clamp01(Vector3.Distance(pos, digPos.ToVector3()) / radius) * 0.25f);
								
								if (getVoxel.Volume > digPower)
									getVoxel.Volume -= (byte)digPower;
								else
					                getVoxel.Volume = 0;
								if(getVoxel.Volume < 128)
								{
									getVoxel.Volume = 0;
									VFVoxelTerrain.self.AlterVoxelInBuild(digPos, new VFVoxel(0,0));
								}
								else
								{
									VFVoxelTerrain.self.AlterVoxelInBuild(digPos, getVoxel);
								}

								if (onDigTerrain != null)
									onDigTerrain(digPos);
							}
						}
					}
				}
			}
		}
		
		if(type == 1 || type == 2)
		{
			for(float i = -radius; i < radius; i += Block45Constants._scale)
			{
				for(float j = -radius; j < radius; j += Block45Constants._scale)
				{
					for(float k = -radius; k < radius; k += Block45Constants._scale)
					{
						Vector3 offsetPos = new Vector3(i, j, k);
//						Vector3 worldPos = BuildBlockManager.BestMatchPosition(pos + offsetPos);
						Vector3 worldPos = pos + offsetPos;
						worldPos.x = Mathf.Floor(worldPos.x * Block45Constants._scaleInverted) * Block45Constants._scale;
						worldPos.y = Mathf.Floor(worldPos.y * Block45Constants._scaleInverted) * Block45Constants._scale;
						worldPos.z = Mathf.Floor(worldPos.z * Block45Constants._scaleInverted) * Block45Constants._scale;

						if(Vector3.Distance(worldPos, pos) < radius)
						{
//							IntVector3 index = BuildBlockManager.WorldPosToBuildIndex(worldPos);
							IntVector3 index = new IntVector3(Mathf.FloorToInt(worldPos.x *  Block45Constants._scaleInverted),
							                                  Mathf.FloorToInt(worldPos.y * Block45Constants._scaleInverted),
							                                  Mathf.FloorToInt(worldPos.z * Block45Constants._scaleInverted));

							B45Block block = Block45Man.self.DataSource.SafeRead(index.x, index.y, index.z);
							if(!BlockVolumes.ContainsKey(index))
								BlockVolumes[index] = 255f;
							
							if((block.blockType >> 2) != 0)
							{
								int ItemID = PEBuildingMan.GetBlockItemProtoID(block.materialType);
								NaturalRes resData = NaturalRes.GetTerrainResData(ItemProto.GetItemData(ItemID).setUp);
								float digPower = power * resData.m_duration * (1f - Mathf.Clamp01(offsetPos.magnitude / radius) * 0.25f);
								BlockVolumes[index] -= digPower;
								if(BlockVolumes[index] <= 0)
								{
									Block45Man.self.DataSource.SafeWrite(new B45Block(0,0), index.x, index.y, index.z, 0);
									BlockVolumes.Remove(index);
//									EffectManager.Instance.Instantiate(47,BuildBlockManager.BuildIndexToWorldPos(index),Quaternion.identity);
									EffectManager.Instance.Instantiate(47,worldPos,Quaternion.identity);
								}
							}
						}
					}
				}
			}
		}
		if(needRefresh)
		{
			if(null != LSubTerrainMgr.Instance)
				LSubTerrainMgr.RefreshAllLayerTerrains();
			else if(null != RSubTerrainMgr.Instance)
				RSubTerrainMgr.RefreshAllLayerTerrains();
		}
	}

    struct Tuple<T1, T2>
    {
        public T1 v1;
        public T2 v2;
    }

    static void GetTreeInfo(Vector3 treePos, ref List<Tuple<Vector3, int>> trees)
    {
        if (LSubTerrainMgr.Instance != null)
        {
            List<GlobalTreeInfo> tree_list = LSubTerrainMgr.Picking(treePos, true);
            if (tree_list.Count > 0)
            {
                foreach (var iter in tree_list)
                {
                    Tuple<Vector3, int> tree;
                    tree.v1 = iter.WorldPos;
                    tree.v2 = iter._treeInfo.m_protoTypeIdx;
                    trees.Add(tree);
                }
            }
        }
        else
        {
            var posTrees = RSubTerrainMgr.Instance.TreesAtPos(treePos);
            foreach (var posTree in posTrees)
            {
                Tuple<Vector3, int> tree;
                tree.v1 = posTree.m_pos;
                tree.v2 = posTree.m_protoTypeIdx;
                trees.Add(tree);
            }
        }
    }

    static void GetTreeInfo(IntVector3 idx, ref List<Tuple<Vector3, int>> trees)
    {
        Vector3 pos = idx;
        GetTreeInfo(pos, ref trees);
        pos.x = idx.x - 1;
        GetTreeInfo(pos, ref trees);
        pos.x = idx.x + 1;
        GetTreeInfo(pos, ref trees);
        pos.x = idx.x;
        pos.z = idx.z - 1;
        GetTreeInfo(pos, ref trees);
        pos.z = idx.z + 1;
        GetTreeInfo(pos, ref trees);
    }

    static void GetGrassPos(IntVector3 idx, ref List<Vector3> grasses)
    {
		Vector3 outPos;
        Vector3 pos = idx;
        if (PeGrassSystem.DeleteAtPos(pos, out outPos))
            grasses.Add(outPos);
        pos.x = idx.x - 1;
		if (PeGrassSystem.DeleteAtPos(pos, out outPos))
			grasses.Add(outPos);
		pos.x = idx.x + 1;
		if (PeGrassSystem.DeleteAtPos(pos, out outPos))
			grasses.Add(outPos);
		pos.x = idx.x;
        pos.z = idx.z - 1;
		if (PeGrassSystem.DeleteAtPos(pos, out outPos))
			grasses.Add(outPos);
		pos.z = idx.z + 1;
		if (PeGrassSystem.DeleteAtPos(pos, out outPos))
			grasses.Add(outPos);
	}
	
	internal static int DigTerrainNetwork(SkNetworkInterface digger, IntVector3 intPos, float durDec, float radius, float resourceBonus, bool returnResource, bool bGetSpItems, float height)
	{
		if (!digger.hasOwnerAuth)
			return 0;
        if (Pathea.PeGameMgr.IsMulti && PlayerNetwork.mainPlayer._curSceneId != (int)Pathea.SingleGameStory.StoryScene.MainLand)
            return 0;
        trees.Clear();
        grasses.Clear();

        byte[] voxelData = PETools.Serialize.Export(w =>
        {
            for (float _x = -radius; _x <= radius; ++_x)
            {
                for (float _z = -radius; _z <= radius; ++_z)
                {
                    for (float _y = -height; _y <= height; ++_y)
                    {
                        IntVector3 idx = new IntVector3(intPos.x + _x, intPos.y + _y, intPos.z + _z);

                        float sqrMagnitude = _x * _x + _y * _y + _z * _z;

                        if (!returnResource && sqrMagnitude > radius * radius)
                            continue;

                        VFVoxel getVoxel = VFVoxelTerrain.self.Voxels.SafeRead(idx.x, idx.y, idx.z);

                        BufferHelper.Serialize(w, getVoxel.Type);
                        BufferHelper.Serialize(w, getVoxel.Volume);

                        GetTreeInfo(idx, ref trees);
                        GetGrassPos(idx, ref grasses);
                    }
                }
            }
        });

        digger.RPCServer(EPacketType.PT_InGame_SKDigTerrain, intPos, durDec, radius, resourceBonus, voxelData, returnResource, bGetSpItems, height);

		DigPlant(intPos.ToVector3(), radius);

		if (trees.Count != 0)
        {
            byte[] treeData = PETools.Serialize.Export(w =>
            {
                BufferHelper.Serialize(w, trees.Count);
                foreach (var tree in trees)
                {
                    BufferHelper.Serialize(w, tree.v1);
                    BufferHelper.Serialize(w, tree.v2);
                }
            });

            digger.RPCServer(EPacketType.PT_InGame_ClearTree, treeData);
        }

        if (grasses.Count != 0)
        {
            byte[] grassData = PETools.Serialize.Export(w =>
            {
                BufferHelper.Serialize(w, grasses.Count);
                foreach (var pos in grasses)
                    BufferHelper.Serialize(w, pos);
            });

            digger.RPCServer(EPacketType.PT_InGame_ClearGrass, grassData);
        }

		return 0;
	}

	internal static void DestroyTerrainInRangeNetwork(int type, SkillRunner caster, Vector3 pos, float power, float radius)
	{
		if (2 == type)
		{
            trees.Clear();
            grasses.Clear();

            IntVector3 basePos = new IntVector3(pos - radius * Vector3.one);

            byte[] voxelData = PETools.Serialize.Export(w =>
            {
                for (int i = 0; i < 2 * radius; i++)
                {
                    for (int j = 0; j < 2 * radius; j++)
                    {
                        for (int k = 0; k < 2 * radius; k++)
                        {
                            IntVector3 idx = new IntVector3(basePos.x + i, basePos.y + j, basePos.z + k);
                            if (Vector3.Distance(idx, pos) <= radius)
                            {
                                VFVoxel getVoxel = VFVoxelTerrain.self.Voxels.SafeRead(idx.x, idx.y, idx.z);

                                BufferHelper.Serialize(w, getVoxel.Type);
                                BufferHelper.Serialize(w, getVoxel.Volume);

                                GetTreeInfo(idx.ToVector3(), ref trees);
                                GetGrassPos(idx, ref grasses);
                            }
                        }
                    }
                }
            });

            if (null != caster)
                caster.RPCServer(EPacketType.PT_InGame_SkillVoxelRange, pos, power, radius, voxelData);

            if (trees.Count != 0)
            {
                byte[] treeData = PETools.Serialize.Export(w =>
                {
                    BufferHelper.Serialize(w, trees.Count);
                    foreach (var tree in trees)
                    {
                        BufferHelper.Serialize(w, tree.v1);
                        BufferHelper.Serialize(w, tree.v2);
                    }
                });

                caster.RPCServer(EPacketType.PT_InGame_ClearTree, treeData);
            }

            if (grasses.Count != 0)
            {
                byte[] grassData = PETools.Serialize.Export(w =>
                {
                    BufferHelper.Serialize(w, grasses.Count);
                    foreach (var grassPos in grasses)
                        BufferHelper.Serialize(w, grassPos);
                });

                caster.RPCServer(EPacketType.PT_InGame_ClearGrass, grassData);
            }
        }

		if (1 == type || 2 == type)
			if (null != caster)
				caster.RPCServer(EPacketType.PT_InGame_SkillBlockRange, pos, power, radius, Block45Constants._scale);
	}

	public static void BlockClearGrass(IBSDataSource ds, IEnumerable<IntVector3> indexes)
	{
		grasses.Clear();

		if (ds == BuildingMan.Blocks)
		{
			foreach (IntVector3 index in indexes)
			{
				Vector3 pos = new Vector3(index.x * ds.Scale, index.y * ds.Scale, index.z * ds.Scale) - ds.Offset;

				Vector3 outPos;
				if (PeGrassSystem.DeleteAtPos(pos, out outPos))
					grasses.Add(outPos);

				pos.y -= 1;

				if (PeGrassSystem.DeleteAtPos(pos, out outPos))
					grasses.Add(outPos);
			}
		}
		else if (ds == BuildingMan.Voxels)
		{
			foreach (IntVector3 index in indexes)
			{
				Vector3 pos = index.ToVector3();

				Vector3 outPos;
				if (PeGrassSystem.DeleteAtPos(pos, out outPos))
					grasses.Add(outPos);

				pos.y -= 1;

				if (PeGrassSystem.DeleteAtPos(pos, out outPos))
					grasses.Add(outPos);
			}
		}

		if (grasses.Count == 0)
			return;

		byte[] grassData = PETools.Serialize.Export(w =>
		{
			w.Write(grasses.Count);
			foreach (Vector3 pos in grasses)
				BufferHelper.Serialize(w, pos);
		});

		PlayerNetwork.mainPlayer.RPCServer(EPacketType.PT_InGame_ClearGrass, grassData);
	}

	internal static float Fell(GlobalTreeInfo treeinfo, float damage, float hp)
	{
		NaturalRes res = NaturalResAsset.NaturalRes.GetTerrainResData(treeinfo._treeInfo.m_protoTypeIdx + 1000);
		if(null != res)
			hp -= damage * res.m_duration / (treeinfo._treeInfo.m_heightScale * treeinfo._treeInfo.m_widthScale);
		return hp;
	}

	internal static void RemoveTree(GlobalTreeInfo treeinfo)
	{
		if(null == treeinfo)
			return;

		if (null != LSubTerrainMgr.Instance)
		{
			LSubTerrainMgr.DeleteTree(treeinfo);
			LSubTerrainMgr.RefreshAllLayerTerrains();
		}
		else if (null != RSubTerrainMgr.Instance)
		{
			RSubTerrainMgr.DeleteTree(treeinfo._treeInfo);
			RSubTerrainMgr.RefreshAllLayerTerrains();
		}
	}

	internal static Dictionary<int, int> GetTreeResouce(GlobalTreeInfo treeinfo, float bouns,bool bGetSpItems = false)
	{
		Dictionary<int, int> itemGet = new Dictionary<int, int>();
		if(null != treeinfo)
		{
			NaturalRes res =  NaturalResAsset.NaturalRes.GetTerrainResData(treeinfo._treeInfo.m_protoTypeIdx + 1000);

			if(null != res)
			{
				float resGet =0;
				if(res.mFixedNum > 0)
					resGet = res.mFixedNum;
				else
					resGet = res.mSelfGetNum * treeinfo._treeInfo.m_widthScale * treeinfo._treeInfo.m_widthScale * treeinfo._treeInfo.m_heightScale * (1 + bouns);

				if(resGet < 1)
					resGet = 1;

				for(int numGet=0;numGet<(int)resGet;numGet++)
				{
					int getPro = UnityEngine.Random.Range(0, 100);
					for(int i=0;i<res.m_itemsGot.Count;i++)
					{
						if(getPro<res.m_itemsGot[i].m_probablity)
						{
							if(itemGet.ContainsKey(res.m_itemsGot[i].m_id))
								itemGet[res.m_itemsGot[i].m_id]++;
							else
								itemGet[res.m_itemsGot[i].m_id] = 1;
							break;
						}
					}
				}
				
				if(res.m_extraGot.extraPercent > 0 && UnityEngine.Random.value < resGet * res.m_extraGot.extraPercent)
				{
					resGet *= res.m_extraGot.extraPercent;
					for(int i = 0; i < resGet; i++)
					{
						int rand = UnityEngine.Random.Range(0, 100);
						for(int j = 0; j < res.m_extraGot.m_extraGot.Count; j++)
						{
							if(rand < res.m_extraGot.m_extraGot[j].m_probablity)
							{
								if(itemGet.ContainsKey(res.m_extraGot.m_extraGot[j].m_id))
									itemGet[res.m_extraGot.m_extraGot[j].m_id]++;
								else
									itemGet[res.m_extraGot.m_extraGot[j].m_id] = 1;
								break;
							}
						}
					}
				}
				if(bGetSpItems)
				{
					if(res.mFixedNum > 0)
						resGet = res.mFixedNum;
					else
						resGet = res.mSelfGetNum * treeinfo._treeInfo.m_widthScale * treeinfo._treeInfo.m_widthScale * treeinfo._treeInfo.m_heightScale * (1 + bouns);
					
					if(resGet < 1)
						resGet = 1;

					if(res.m_extraSpGot.extraPercent > 0 && UnityEngine.Random.value < resGet * res.m_extraSpGot.extraPercent)
					{
						resGet *= res.m_extraSpGot.extraPercent;
						for(int i = 0; i < resGet; i++)
						{
							int rand = UnityEngine.Random.Range(0, 100);
							for(int j = 0; j < res.m_extraSpGot.m_extraGot.Count; j++)
							{
								if(rand < res.m_extraSpGot.m_extraGot[j].m_probablity)
								{
									if(itemGet.ContainsKey(res.m_extraSpGot.m_extraGot[j].m_id))
										itemGet[res.m_extraSpGot.m_extraGot[j].m_id]++;
									else
										itemGet[res.m_extraSpGot.m_extraGot[j].m_id] = 1;
									break;
								}
							}
						}
					}
				}
			}
		}
		return itemGet;
	}

	public static void DigTerrainNetReturn(IntVector3 pos, float durDec, float radius, float height, bool bReturnItem)
	{
		for (float _x = -radius; _x <= radius; ++_x)
		{
			for (float _z = -radius; _z <= radius; ++_z)
			{
				for (float _y = -height; _y <= height; ++_y)
				{
					IntVector3 idx = new IntVector3(pos.x + _x, pos.y + _y, pos.z + _z);

					float sqrMagnitude = _x * _x + _y * _y + _z * _z;

					if (!bReturnItem && sqrMagnitude > radius * radius)
						continue;

					VFVoxel voxel = VFVoxelTerrain.self.Voxels.SafeRead(idx.x, idx.y, idx.z);
					if (voxel.Volume == 0)
						continue;

					float digPower = durDec;
					NaturalRes resData = NaturalRes.GetTerrainResData((int)voxel.Type);
					if (null != resData)
						digPower *= resData.m_duration;

					if (digPower >= 255)
						voxel.Volume = 0;
					else if (voxel.Volume > digPower)
						voxel.Volume -= (byte)digPower;
					else
						voxel.Volume = 0;

					if (voxel.Volume <= 127)
						voxel.Volume = 0;

                    ApplyBSDataFromNet(0, idx, new BSVoxel(voxel));
                    DeleteTree(idx);
                    DeleteGrass(idx);

					if (onDigTerrain != null)
						onDigTerrain(idx);
				}
			}
		}

        if (LSubTerrainMgr.Instance != null)
            LSubTerrainMgr.RefreshAllLayerTerrains();
        else if (RSubTerrainMgr.Instance != null)
            RSubTerrainMgr.RefreshAllLayerTerrains();
    }

    static void DeleteTree(IntVector3 idx)
    {
        Vector3 pos = idx;
        DeleteTree(pos);
        pos.x = idx.x - 1;
        DeleteTree(pos);
        pos.x = idx.x + 1;
        DeleteTree(pos);
        pos.x = idx.x;
        pos.z = idx.z - 1;
        DeleteTree(pos);
        pos.z = idx.z + 1;
        DeleteTree(pos);
    }

    public static void DeleteTree(Vector3 treePos)
    {
        if (LSubTerrainMgr.Instance != null)
        {
            LSubTerrainMgr.DeleteTreesAtPos(treePos);
            LSubTerrSL.AddDeletedTree(treePos);
        }
        else if (RSubTerrainMgr.Instance != null)
        {
            RSubTerrainMgr.DeleteTreesAtPos(treePos);
            RSubTerrSL.AddDeletedTree(treePos);
        }
    }

    public static void CacheDeleteTree(List<Vector3> positions)
    {
        if (Pathea.PeGameMgr.IsMultiCustom || Pathea.PeGameMgr.IsMultiStory)
        {
			foreach (var pos in positions)
				LSubTerrSL.AddDeletedTree(pos);
		}
        else
        {
			foreach (var pos in positions)
				RSubTerrSL.AddDeletedTree(pos);
		}
    }

    static void DeleteGrass(IntVector3 idx)
    {
        Vector3 pos = idx;
        DeleteGrass(pos);
        pos.x = idx.x - 1;
        DeleteGrass(pos);
        pos.x = idx.x + 1;
        DeleteGrass(pos);
        pos.x = idx.x;
        pos.z = idx.z - 1;
        DeleteGrass(pos);
        pos.z = idx.z + 1;
        DeleteGrass(pos);
    }

    public static void DeleteGrass(Vector3 pos)
    {
        if (!PeGrassSystem.DeleteAtPos(pos))
            GrassDataSL.AddDeletedGrass(pos);
    }

    public static void CacheDeleteGrass(List<Vector3> positions)
    {
		foreach (var pos in positions)
			GrassDataSL.AddDeletedGrass(pos);
	}

    public static void TerrainDestroyInRangeNetReturn(Vector3 pos, float power, float radius)
	{
		IntVector3 basePos = new IntVector3(pos - radius * Vector3.one);
		
		for (int i = 0; i < 2 * radius; i++)
		{
			for (int j = 0; j < 2 * radius; j++)
			{
				for (int k = 0; k < 2 * radius; k++)
				{
					IntVector3 digPos = new IntVector3(basePos.x + i, basePos.y + j, basePos.z + k);
					if (Vector3.Distance(digPos, pos) < radius)
					{
						VFVoxel voxel = VFVoxelTerrain.self.Voxels.SafeRead(digPos.x, digPos.y, digPos.z);

                        DeleteTree(digPos.ToVector3());
                        DeleteGrass(digPos.ToVector3());
						
						if (voxel.Volume > 0)
						{
							NaturalRes resData = NaturalRes.GetTerrainResData(voxel.Type);
							if (null != resData)
							{
								float digPower = power * resData.m_duration * (1f - Mathf.Clamp01(Vector3.Distance(pos, digPos.ToVector3()) / radius) * 0.25f);
								
								if (digPower >= 255)
									voxel.Volume = 0;
								else if (voxel.Volume > digPower)
									voxel.Volume -= (byte)digPower;
								else
									voxel.Volume = 0;
								
								if (voxel.Volume <= 127)
									voxel.Volume = 0;
                                
                                ApplyBSDataFromNet(0, digPos, new BSVoxel(voxel));
							}
							
							if (onDigTerrain != null)
								onDigTerrain(digPos);
						}
					}
				}
			}
		}

		if(LSubTerrainMgr.Instance != null)
			LSubTerrainMgr.RefreshAllLayerTerrains();
		else if(RSubTerrainMgr.Instance != null)
			RSubTerrainMgr.RefreshAllLayerTerrains();
	}

	public static void ApplyVoxelData(byte[] data)
	{
		PETools.Serialize.Import(data, br =>
		{
			int count = br.ReadInt32();

			for (int i = 0; i < count; i++)
			{
				IntVector3 pos;
				BufferHelper.ReadIntVector3(br, out pos);
				BSVoxel voxel;
				BufferHelper.ReadBSVoxel(br, out voxel);
                ApplyBSDataFromNet(0, pos, voxel);

				if (voxel.volmue < 128)
				{
					if (onDigTerrain != null)
						onDigTerrain(pos);
				}
				else
				{
					DirtyTerrain(pos, voxel.ToVoxel(), voxel.type);
				}
            }
		});

		if(RSubTerrainMgr.Instance != null)
			RSubTerrainMgr.RefreshAllLayerTerrains();
        if (LSubTerrainMgr.Instance != null)
            LSubTerrainMgr.RefreshAllLayerTerrains();
	}

    public static void BlockDestroyInRangeNetReturn(byte[] data)
    {
        PETools.Serialize.Import(data, r =>
        {
            int count = BufferHelper.ReadInt32(r);

			for (int i = 0; i < count; i++)
			{
				IntVector3 pos;
				BufferHelper.ReadIntVector3(r, out pos);
				BSVoxel voxel;
				BufferHelper.ReadBSVoxel(r, out voxel);
                ApplyBSDataFromNet(1, pos, voxel);
            }
        });
    }

	public static void ApplyBlockData(byte[] data)
	{
		PETools.Serialize.Import(data, br =>
		{
			int count = br.ReadInt32();

			for (int i = 0; i < count; i++)
			{
				IntVector3 pos;
				BufferHelper.ReadIntVector3(br, out pos);
				BSVoxel voxel;
				BufferHelper.ReadBSVoxel(br, out voxel);
				ApplyBSDataFromNet(1, pos, voxel);
			}
		});
	}

	public static void ApplyBSVoxelData(byte[] binData)
	{
        PETools.Serialize.Import(binData, r =>
        {
            int dsType = BufferHelper.ReadInt32(r);
            int count = BufferHelper.ReadInt32(r);

			//IBSDataSource ds = BuildingMan.Datas[dsType];

			for (int i = 0; i < count; i++)
            {
				IntVector3 pos;
				BufferHelper.ReadIntVector3(r, out pos);
				BSVoxel newVoxel;
				BufferHelper.ReadBSVoxel(r, out newVoxel);
                ApplyBSDataFromNet(dsType, pos, newVoxel);

				//if (dsType == 1)
				//{
				//	Vector3 grassPos = new Vector3(pos.x * ds.Scale, pos.y * ds.Scale, pos.z * ds.Scale) - ds.Offset;
				//	PeGrassSystem.DeleteAtPos(grassPos);
				//	grassPos.y -= 1;
				//	PeGrassSystem.DeleteAtPos(grassPos);
				//}
				//else if (dsType == 0)
				//{
				//	Vector3 grassPos = pos;
				//	PeGrassSystem.DeleteAtPos(grassPos);
				//	grassPos.y -= 1;
				//	PeGrassSystem.DeleteAtPos(grassPos);
				//}
			}
        });
    }

    public static void DirtyTerrain(IntVector3 pos, VFVoxel voxel, byte targetType)
    {
        VFVoxel prv_voxel = VFVoxelTerrain.self.Voxels.SafeRead(pos.x, pos.y + 1, pos.z);
        if (prv_voxel.Volume < 128)
        {
            if (voxel.Volume >= 128)
            {
                if (onDirtyVoxel != null)
                    onDirtyVoxel(pos, targetType);
            }
        }
    }

    public static void ApplyBSDataFromNet(int dsType, IntVector3 pos, BSVoxel voxel)
    {
		if (dsType == 0)
	    {
			var effectChunk = VFVoxelChunkData.GetDirtyChunkPosListMulti(pos.x, pos.y, pos.z);
	        foreach (IntVector3 chunkPos in effectChunk)
				ChunkManager.ApplyVoxelVolume(pos, chunkPos, voxel.ToVoxel());
	    }

		BuildingMan.Datas[dsType].Write(voxel, pos.x, pos.y, pos.z);
    }
}
