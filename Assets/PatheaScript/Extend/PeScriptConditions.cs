using PatheaScript;
using System.Collections.Generic;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Event = PatheaScript.Event;
using ItemAsset.PackageHelper;

namespace PatheaScriptExt
{
    public class CPlayerOwn : Condition
    {
        VarRef mPlayerId;
        VarRef mItemId;
        VarRef mItemCount;
        Compare mCompare;

        public override bool Parse()
        {
            mPlayerId = PeType.GetPlayerId(mInfo, mTrigger);

            mItemId = PeType.GetItemId(mInfo, mTrigger);
            
            mItemCount = PatheaScript.Util.GetVarRefOrValue(mInfo, "count", VarValue.EType.Int, mTrigger);            

            mCompare = mFactory.GetCompare(mInfo, "compare");

            return true;
        }

        public override bool Do()
        {
            List<Pathea.PeEntity> list = PeType.GetPlayer((int)mPlayerId.Value);

            bool ret = true;
            foreach (Pathea.PeEntity p in list)
            {
                Pathea.PlayerPackageCmpt pkg = p.GetCmpt<Pathea.PlayerPackageCmpt>();
                int hasCount = pkg.package.GetCount((int)mItemId.Value);
                if(false == mCompare.Do(hasCount, mItemCount.Value))
                {
                    ret = false;
                }
            }

            return ret;
        }

        public override string ToString()
        {
            return string.Format("Condition[Is player:{0} item:{1} count:{2} compare:{3} ref:{4}]", 0, mItemId, mItemCount, mCompare, mItemCount);
        }
    }

    public class CStopWatch : Condition
    {
        VarRef mTimerName;
        Compare mCompare;
        VarRef mRef;

        public override bool Parse()
        {
            mTimerName = PatheaScript.Util.GetVarRefOrValue(mInfo, "id", VarValue.EType.String, mTrigger);
            mCompare = mFactory.GetCompare(mInfo, "compare");
            mRef = PatheaScript.Util.GetVarRefOrValue(mInfo, "sec", VarValue.EType.Float, mTrigger);

            return true;
        }

        public override bool Do()
        {
            if (false == base.Do())
            {
                return false;
            }

            PETimer timer = PeTimerMgr.Instance.Get((string)mTimerName.Value);
            if (null == timer)
            {
                return false;
            }

            VarValue second = new VarValue((float)timer.Second);

            return mCompare.Do(second, mRef.Value);
        }

    }

    public class CPlayerKillMonster : Condition
    {
        VarRef mCount;
        VarRef mMonsterId;
        Compare mCompare;

        VarValue mKillCount = new VarValue(0);

        void Handler(object sender, PeEvent.KillEventArg arg)
        {
            if (arg.victim.Id == mMonsterId.Value)
            {
                mKillCount += 1;                
            }
        }

        public override bool Parse()
        {
            mCompare = mFactory.GetCompare(mInfo, "compare");

            mCount = PatheaScript.Util.GetVarRefOrValue(mInfo, "count", VarValue.EType.Int, mTrigger);

            mMonsterId = PeType.GetMonsterId(mInfo, mTrigger);

            PeEvent.Globle.kill.Subscribe(Handler);

            return true;
        }

        public override bool Do()
        {
            if (false == base.Do())
            {
                return false;
            }

            return mCompare.Do(mCount.Value, mKillCount);
        }
    }
}