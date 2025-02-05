using UnityEngine;
using ScenarioRTL;
using Pathea;

namespace PeCustom
{
    [Statement("CHANGE EQUIP")]
    public class ChangeEquipListener : ScenarioRTL.EventListener
    {
        // 在此列举参数
        
        // 在此初始化参数
        protected override void OnCreate()
        {
        
        }
        
		// 打开事件监听
		public override void Listen()
		{
			PeEntity mainPlayer = PeCreature.Instance.mainPlayer;

			if (mainPlayer != null)
			{
				mainPlayer.equipmentCmpt.OnEquipmentChange += Post;
			}
			else
			{
				Debug.LogError("main player is null");
			}
		}

		// 关闭事件监听
		public override void Close()
		{
			PeEntity mainPlayer = PeCreature.Instance.mainPlayer;

			if (mainPlayer != null)
			{
				mainPlayer.equipmentCmpt.OnEquipmentChange -= Post;
			}
			else
			{
				Debug.LogError("main player is null");
			}
		}
    }
}
