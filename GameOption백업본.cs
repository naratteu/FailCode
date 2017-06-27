using UnityEngine;
using System.Collections.Generic;
using System;
using System.IO;
using System.Text;
using Boomlagoon.JSON;

#if NOT_USE
public enum eBattleSpeedOption
{
    SlOW, NORMAL, FAST
}
#endif

/// <summary>
/// 게임 옵션 - UI로 노출되는 옵션 외에 파일로 노출되는 옵션 
/// </summary>
public static class GameOption
{
    public enum SetType
    {
        //이하 소리
        bgmMute,
        sfxMute,
        bgmVolume,
        sfxVolume,
        //이하 프레임
        normalFrameRate,
        battleFrameRate,
        //이하 퀼리티 레벨, 임시
        QL,
        //이하 진동
        workCompleteVibrate,
        battleEndVibrate,
        //이하 푸시
        push_CONSTRUCT_OR_UPGRADE,  //건설, 강화 완료
        push_RESEARCH,              //연구
        push_PRODUCE_UNIT,          //병력 생산.
        push_TERRITORY_BATTLE,      //영지 쟁탈전 상황           //서버?
        push_COLLECT_RESOURCE,      //자원 수거
        push_TERRITORY_REWARD,      //점령지 보상
        push_ALLIANCE_BATTLE,       //동맹 쟁탈전 참여 알림
        push_RAID_MODE,             //레이드 모드 참여 알림      //서버?
        push_FREE_SUMMON_HERO,      //무료 영웅 소환.
        push_EVENT_OR_NOTICE,       //이벤트 / 공지              //서버?
    }

    private static string OPTION_DATA { get { return "OPTION_DATA"; } } //유저인덱스 추가 필요시 여기에서 처리

    static JSONObject mRawData = new JSONObject();
    public static void SetRawData(JSONObject jObj)
    {
        mRawData = jObj;

        //JSON을 새로 가져오면 모든 셋팅요소들 값변경-갱신 처리
        foreach (KeyValuePair<SetType, Settable> kv in SettableList)
            kv.Value.OnValueChange();
    }

    /// <summary>
    /// 세팅가능한 요소들을 정리
    /// </summary>
    public abstract class Settable
    {
        internal string mKey;

        public Settable(SetType type)
        {
            mKey = type.ToString();
            SettableList.Add(type, this);
        }

        public abstract void OnValueChange();
    }
    public class Settable<T> : Settable where T : struct
    {
        Action<T> mOnValueChange;
        public Settable(SetType type, T defaultValue, Action<T> onValueChange = null)
            : base(type)
        {
            mOnValueChange = onValueChange;

            //기존 저장내용이 없을경우 기본값 지정
            if (!mRawData.ContainsKey(mKey))
                Value = defaultValue;
        }

        public T Value
        {
            get
            {
                switch (Type.GetTypeCode(typeof(T)))
                {
                    case TypeCode.Double:   return (T)(object)mRawData.GetNumber(mKey);
                    case TypeCode.Single:   return (T)(object)(float)mRawData.GetNumber(mKey);
                    case TypeCode.Int32:    return (T)(object)(int)mRawData.GetNumber(mKey);
                    case TypeCode.Boolean:  return (T)(object)mRawData.GetBoolean(mKey);
                }
                HwLog.LogError(eLogType.ERROR, typeof(T) + "타입은 지원하지 않습니다.");
                return default(T);
            }
            set
            {
                object o = value;
                JSONValue v = null;

                switch (Type.GetTypeCode(value.GetType()))
                {
                    case TypeCode.Double:   v = (double)o;  break;
                    case TypeCode.Single:   v = (float)o;   break;
                    case TypeCode.Int32:    v = (int)o;     break;
                    case TypeCode.Boolean:  v = (bool)o;    break;
                    default: HwLog.LogError(eLogType.ERROR, typeof(T) + "타입은 지원하지 않습니다."); break;
                }

                mRawData.Add(mKey, v);
                OnValueChange();
            }
        }
        //public void Set(float value) { Value = value; }
        public override void OnValueChange()
        {
            if (mOnValueChange != null) mOnValueChange(Value);
        }
    }
    
    static Dictionary<SetType, Settable> SettableList = new Dictionary<SetType, Settable>();//SetType으로 Settable을 찾고싶다면 이걸 Dictionary로
    public static Settable<T> GetSettable<T>(this SetType type) where T : struct
    {
        Settable<T> st = GetSettable(type) as Settable<T>;
        return st;
    }
    public static Settable GetSettable(this SetType type)
    {
        Settable s;
        SettableList.TryGetValue(type, out s);
        return s;
    }

    public static Settable<bool> bgmMute = new Settable<bool>(SetType.bgmMute, false, AudioManager.SetBGMMute);
    public static Settable<bool> sfxMute = new Settable<bool>(SetType.sfxMute, false, AudioManager.SetSFXMute);
    public static Settable<float> bgmVolume = new Settable<float>(SetType.bgmVolume, 1f, AudioManager.SetBGMVolume);
    public static Settable<float> sfxVolume = new Settable<float>(SetType.sfxVolume, 1f, AudioManager.SetSFXVolume);
    public static Settable<int> normalFrameRate = new Settable<int>(SetType.normalFrameRate, FrameRateManager.DEFAULT_FRAME_RATE, FrameRateType.NORMAL.GetSetFrameRate());
    public static Settable<int> battleFrameRate = new Settable<int>(SetType.battleFrameRate, FrameRateManager.DEFAULT_FRAME_RATE, FrameRateType.BATTLE.GetSetFrameRate());
    public static Settable<int> ql = new Settable<int>(SetType.QL, QualitySettings.antiAliasing); //QualitySettings.SetQualityLevel(QualitySettings.GetQualityLevel);
    public static Settable<bool> workCompleteVibrate = new Settable<bool>(SetType.workCompleteVibrate, true); //전투 종료 진동
    public static Settable<bool> battleEndVibrate = new Settable<bool>(SetType.battleEndVibrate, true); //작업완료 진동
    public static Settable<bool> push_CONSTRUCT_OR_UPGRADE      = new Settable<bool>(SetType.push_CONSTRUCT_OR_UPGRADE  , true, GetChangePushFlagCallback(ePushKind.CONSTRUCT_OR_UPGRADE));
    public static Settable<bool> push_RESEARCH                  = new Settable<bool>(SetType.push_RESEARCH              , true, GetChangePushFlagCallback(ePushKind.RESEARCH            ));
    public static Settable<bool> push_PRODUCE_UNIT              = new Settable<bool>(SetType.push_PRODUCE_UNIT          , true, GetChangePushFlagCallback(ePushKind.PRODUCE_UNIT        ));
    public static Settable<bool> push_TERRITORY_BATTLE          = new Settable<bool>(SetType.push_TERRITORY_BATTLE      , true, GetChangePushFlagCallback(ePushKind.TERRITORY_BATTLE    ));
    public static Settable<bool> push_COLLECT_RESOURCE          = new Settable<bool>(SetType.push_COLLECT_RESOURCE      , true, GetChangePushFlagCallback(ePushKind.COLLECT_RESOURCE    ));
    public static Settable<bool> push_TERRITORY_REWARD          = new Settable<bool>(SetType.push_TERRITORY_REWARD      , true, GetChangePushFlagCallback(ePushKind.TERRITORY_REWARD    ));
    public static Settable<bool> push_ALLIANCE_BATTLE           = new Settable<bool>(SetType.push_ALLIANCE_BATTLE       , true, GetChangePushFlagCallback(ePushKind.ALLIANCE_BATTLE     ));
    public static Settable<bool> push_RAID_MODE                 = new Settable<bool>(SetType.push_RAID_MODE             , true, GetChangePushFlagCallback(ePushKind.RAID_MODE           ));
    public static Settable<bool> push_FREE_SUMMON_HERO          = new Settable<bool>(SetType.push_FREE_SUMMON_HERO      , true, GetChangePushFlagCallback(ePushKind.FREE_SUMMON_HERO    ));
    public static Settable<bool> push_EVENT_OR_NOTICE           = new Settable<bool>(SetType.push_EVENT_OR_NOTICE       , true, GetChangePushFlagCallback(ePushKind.EVENT_OR_NOTICE     ));

#if NOT_USE

    //푸시 옵션 텝.
    private static bool mWorkCompletePush;              //작업 완료 푸시
    private static bool mConfrontPush;                  //대치 알림 푸시
    private static bool mSystemPush;                    //시스템 푸시
    private static bool mPushTimeLock;                  //푸시 시간 락
    private static int mMinStep;
    private static int mMaxStep;

    private static bool mChangeOption = false;          //옵션 변경됨.
    public static bool IsChangedOption { get { return mChangeOption; } }

    public static eBattleSpeedOption BattleSpeed;
    public static bool WorkCompletePush { get { return mWorkCompletePush; } }
    public static bool ConfrontPush { get { return mConfrontPush; } }
    public static bool SystemPush { get { return mSystemPush; } }
    public static bool PushTimeLock { get { return mPushTimeLock; } }
    public static int TimeMinStep { get { return mMinStep; } }
    public static int TimeMaxStep { get { return mMaxStep; } }
#endif

    /// <summary>
    /// 옵션 초기화.
    /// </summary>
    public static void Initialize()
    {
        //옵션이 계정귀속이라면 키에 유저값도 삽입 필요
        if (PlayerPrefs.HasKey(OPTION_DATA))
        {
            string text = PlayerPrefs.GetString(OPTION_DATA);
            JSONObject jo = JSONObject.Parse(text);
            if(jo != null) SetRawData(jo);
        }

#if NOT_USE

        //푸시옵션
        mWorkCompletePush = PlayerPrefs.GetInt(Constants.WORK_COMPLETE_PUSH_NOTICE, 1) == 1 ? true : false;
        mConfrontPush = PlayerPrefs.GetInt(Constants.CONFRONT_PUSH_NOTICE, 1) == 1 ? true : false;
        mSystemPush   = PlayerPrefs.GetInt(Constants.SYSTEM_PUSH_NOTICE, 1) == 1 ? true : false;

        mPushTimeLock = PlayerPrefs.GetInt(Constants.CHECK_DENIE_PUSH, 1) == 1 ? true : false;

        mMinStep = PlayerPrefs.GetInt(Constants.DENIE_MIN_TIME, 0);
        mMaxStep = PlayerPrefs.GetInt(Constants.DENIE_MAX_TIME, 24);
#endif
        //별도의 플레이어프렢 키를 쓰는데 통합 필요한지
        //mWorkCompleteVibrate = PlayerPrefs.GetInt(Constants.COMPLETE_VIBRATE, 1) == 1 ? true : false;
        //mBattleEndVibrate    = PlayerPrefs.GetInt(Constants.BATTLE_END_VIBRATE, 1) == 1 ? true : false;
        //Constants에 있는거 제거?
    }

    /// <summary>
    /// 옵션 JSON 저장
    /// </summary>
    public static void SaveOption(Action endCall)
    {
        //클라에 저장
        PlayerPrefs.SetString(OPTION_DATA, mRawData.ToString());
        PlayerPrefs.Save();

        //서버에 저장(푸시무시등...)
        //...
        //서버통신 완료 후 엔드콜
        if (endCall != null) endCall();
    }
#if NOT_USE

    public static void SetBattleSpeed(eBattleSpeedOption speed)
    {
        //if (mBattleSpeed != speed)
        //{
        //    mBattleSpeed = speed;
        //    PlayerPrefs.SetInt(Constants.BATTLE_SPEED, (int)speed);
        //}
    }


    public static void SetMinTime(int iMin)
    {
        if (mMinStep != iMin)
        {
            mMinStep = iMin;
            PlayerPrefs.SetInt(Constants.DENIE_MIN_TIME, iMin);
        }
    }

    public static void SetMaxTime(int iMax)
    {
        if(mMaxStep != iMax)
        {
            mMaxStep = iMax;
            PlayerPrefs.SetInt(Constants.DENIE_MAX_TIME, iMax);
        }
    }

    public static void SetWorkCompletePush(bool bSet)
    {
        if (mWorkCompletePush != bSet)
        {
            mWorkCompletePush = bSet;

            PlayerPrefs.SetInt(Constants.WORK_COMPLETE_PUSH_NOTICE,
                bSet ? 1 : 0);
        }
    }

    public static void SetConfrontPush(bool bSet)
    {
        if(mConfrontPush != bSet)
        {
            mConfrontPush = bSet;

            PlayerPrefs.SetInt(Constants.CONFRONT_PUSH_NOTICE,
                bSet ? 1 : 0);
        }
    }

    public static void SetSystemPush(bool bSet)
    {
        if(mSystemPush != bSet)
        {
            mSystemPush = bSet;

            PlayerPrefs.SetInt(Constants.SYSTEM_PUSH_NOTICE,
                bSet ? 1 : 0);
        }
    }
#endif

    static Action<bool> GetChangePushFlagCallback(ePushKind k)
    {
        return (b) => PushManager.Instance.SetUsePush(k, b);
    }

#if 퀼리티세팅에서_쓰는거
    void a()
    {
        //퀼리티세팅
        public static int vSyncCount { get; set; } // 프레임레이트 설정하기전에 꼭 해줘야한다고?
        //QualitySettings.vSyncCount = 0;
        //Application.targetFrameRate = 30;
        //이런식
        public static int antiAliasing { get; set; }
        //     ///
        public static BlendWeights blendWeights { get; set; }//뼈갯수?
        public static int masterTextureLimit { get; set; } // 텍스쳐 품질 떨구기
        //     ///
        public static Vector3 shadowCascade4Split { get; set; }
        public static ShadowQuality shadows { get; set; }
        public static ShadowProjection shadowProjection { get; set; }
        public static float shadowDistance { get; set; }
        public static ShadowResolution shadowResolution { get; set; }
        public static float shadowNearPlaneOffset { get; set; }
        public static int shadowCascades { get; set; }
        //public static void SetQualityLevel(int index, [DefaultValue("true")] bool applyExpensiveChanges);
    }
#endif
}