using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class UIWinChatting : UIWindow
{

    enum TapType
    {
        TOTAL,
        ALLIANCE,
    }

    public UIPanel[] mDynamicDepthPanel;//깊이 우선순위는 먼젓게 작은값

    public GameObject mChatListRoot;
    public GameObject mChatBtnRoot;
    public SimpleChatUI mSimpleChatRoot;

    public GameObject[] mNewIcon;

    public UIInput mUIInput;

    public UITable table;//테이블정렬로는 스크롤 안변함.
    public UIScrollView scroll;//스크롤초기화 하면 맨 아래로 내려감.

    public GameObject mPref_ChatItemSet;

    private List<ChatItemSetUI> mChatItems = new List<ChatItemSetUI>(new ChatItemSetUI[ChatManager.CHAT_MAX_COUNT]);
    private ChatItemSetUI LastChat { get { return LastChatID > -1 ? mChatItems[LastChatID] : null; } }
    private int LastChatID = -1;

    void NewChat(ChatItem item)
    {
        LastChatID = NewChat(item, LastChatID, mChatItems);
    }

    int NewChat(ChatItem item, int lastID, List<ChatItemSetUI> currList)
    {
        lastID = (lastID + 1) % ChatManager.CHAT_MAX_COUNT;
        if (currList[lastID] == null)
        {
            GameObject go = NGUITools.AddChild(table.gameObject, mPref_ChatItemSet);
            currList[lastID] = go.GetComponent<ChatItemSetUI>();
        }

        currList[lastID].Set(item);
        return lastID;
    }
#if ALLIANCE_CHAT
    TapType mCurrTap = TapType.TOTAL;
    TapType CurrTap
    {
        get { return mCurrTap; }
        set
        {
            mCurrTap = value;

            ChatAllRefresh();
        }
    }
    
    void Start()
    {
        IsChatOpen = false;
        UpdateNewIcon();
    }

    void OnEnable()
    {
        //1분마다 시간표기 갱신
        StartCoroutine(RefreshChatTime());

        //sleep중 추가된것 체크
        OnChatListUpdate();
    }

    IEnumerator RefreshChatTime()
    {
        while (true)
        {
            yield return new WaitForSeconds(60);

            mChatItems.ForEach((item) => { if (item != null) item.CurrItem.UpdateTime(); });
        }
    }

    bool IsChatOpen
    {
        get { return NGUITools.GetActive(mChatListRoot); }
        set
        {
            if(value)
            {
                //채팅 열어보니 동맹탭이다 -> 동맹채팅 본걸로 처리 New 제거
                if(CurrTap == TapType.ALLIANCE)
                {
                    ChatManager.Instance.UpdateLastSeenChat();
                    UpdateNewIcon();
                }
            }

            NGUITools.SetActive(mChatListRoot, value);

            if (IsChatOpen)
            {
                //바로 리셋하면 버그생겨서 한프레임 쉬고 리셋
                StartCoroutine(Util_RunNextFrame(
                    () =>
                    {
                        table.Reposition();
                        scroll.ResetPosition();

                        scroll.verticalScrollBar.value = 1f;
                    }));
            }
        }
    }

    /// <summary>
    /// 스크롤을 맨 밑으로 내려뒀는지
    /// </summary>
    private bool IsEndScroll
    {
        get
        {
            ChatItemSetUI lastchat = LastChat;
            if (lastchat == null) return true;

            Bounds bounds = NGUIMath.CalculateRelativeWidgetBounds(scroll.transform, lastchat.transform);

            Vector3 min = bounds.min;
            Vector3 max = bounds.max;

            Vector3 offset = scroll.panel.CalculateConstrainOffset(min, max);
            if (offset.sqrMagnitude > 10)
            {
                return false;
            }
            return true;
        }
    }

    public override void OnCreate() { }
    public override void Refresh() { }
    public override void UpdateTick(float aDeltaTime) { }

    public void Set(CallChatUI data)
    {
        bool isOnlySimpleChat = data.IsOnlySimpleChat;
        int depth = data.targetDepth;

        NGUITools.SetActive(mChatBtnRoot, !isOnlySimpleChat);
        if (isOnlySimpleChat)
            IsChatOpen = false;

        //패널깊이 일괄설정. 순서대로 패널깊이가 커짐.
        //panel.depth = depth;아래에서함.
        for(int i=0,l=mDynamicDepthPanel.Length;i<l;i++)
            mDynamicDepthPanel[i].depth = depth + i;
    }

    public void OnToggle_Alliance(UIToggle toggle)
    {
        CurrTap = toggle.value ? TapType.ALLIANCE : TapType.TOTAL;

        //동맹탭으로 바꿧다-> 동맹채팅 본걸로 처리 New 제거
        if (CurrTap == TapType.ALLIANCE)
        {
            ChatManager.Instance.UpdateLastSeenChat();
            UpdateNewIcon();
        }
    }

    public void onClick_Tap_Alliance()
    {
        CurrTap = TapType.ALLIANCE;
    }

    public void onClick_Tap_Total()
    {
        CurrTap = TapType.TOTAL
    }

    public void OnChatListUpdate(bool isUpdateNowChat = false)
    {
        bool isEndScroll = IsEndScroll;
        ChatItemSetUI lc = null;
        Vector3 originalPosition = Vector3.zero;

        List<ChatItem> tList = ChatManager.Instance.TotalChatList;
        List<ChatItem> aList = ChatManager.Instance.AllianceChatList;

        //아무것도 없으면 종료
        if(tList.Count <= 0) return;

        if(isUpdateNowChat)
        {
            //마지막 하나만 추가

            //위치백업
            lc = LastChat;
            if(lc != null && !lc.IsNull)
            {
                originalPosition = lc.transform.position;
            }
            
            ChatItem currItem = tList[tList.Count - 1];
            if(CurrTap == TapType.ALLIANCE)
            {
                if(currItem.channel_type == ChatChannelType.ALLIANCE)
                {
                    //동맹에선 동맹만 추가
                    NewChat(currItem);
                }
            }
            else
            {
                //전체에선 무조건추가
                NewChat(currItem);
            }

            //심플챗 보이기
            bool isShowSimpleChat =
                !currItem.IsMe &&       //남채팅만 보여줌
                !currItem.IsBlock &&    //블록안된 메세지만 보여줘
                currItem.event_type == ChatEventType.CHAT; //그냥 채팅만 보여줘
            if (isShowSimpleChat)
                mSimpleChatRoot.OnNewChat(currItem);
        }
        else
        {
            ChatAllRefresh();
        }
            
        //New아이콘 갱신
        UpdateNewIcon();

        StartCoroutine(Util_RunNextFrame(() => 
        {
            //테이블 다 추가했으니깐 갱신한번해주기
            table.Reposition();
        
            if (isEndScroll)
            {
                //갱신전부터 맨 밑을 보고있었다면 스크롤 리셋
                scroll.ResetPosition();
                scroll.verticalScrollBar.value = 1f;
            }
            else
            {
                //위치복원
                if(originalPosition != Vector3.zero)
                {
                    if(!lc.IsNull)
                    {
                        Vector3 v = originalPosition - lc.transform.position;
                        table.transform.Translate(v);
                    }
                }
            }
        }));
    }

    //전체갱신
    public void ChatAllRefresh(bool scrollReset = true)
    {
        NGUITools.SetActive(table.gameObject, false);

        //일단 전부 비우기
        ChatAllClear();

        List<ChatItem> currData = null;
        switch(mCurrTap)
        {
            case TapType.ALLIANCE : currData = ChatManager.Instance.AllianceChatList; break;
            case TapType.TOTAL : currData = ChatManager.Instance.TotalChatList; break;
        }

        //해당 리스트로 전체 추가
        currData.ForEach(item => { NewChat(item); });

        StartCoroutine(Util_RunNextFrame(
            () => 
            {
                NGUITools.SetActive(table.gameObject, true);
                if (scrollReset)
                {
                    table.Reposition();
                    scroll.ResetPosition();
                    scroll.verticalScrollBar.value = 1f;
                }
            },
            () =>
            {
                if (scrollReset)
                {
                    table.Reposition();
                    scroll.ResetPosition();
                    scroll.verticalScrollBar.value = 1f;
                }

            }));
    }
    
    //전체지우기
    void ChatAllClear()
    {
        mChatItems.ForEach(item => { if(item != null)item.Destroy(); });
    }

    /// <summary>
    /// 채팅리스트를 보여줌
    /// </summary>
    public void OnClick_ChattingOn()
    {
        IsChatOpen = true;
    }

    /// <summary>
    /// 채팅리스트를 숨겨줌
    /// </summary>
    public void OnClick_ChattingOff()
    {
        IsChatOpen = false;
    }

    /// <summary>
    /// 모바일 키보드를 띄움
    /// </summary>
    public void OnClick_OpenKeyboard()
    {
        mUIInput.isSelected = true;
    }

    public void UpdateNewIcon()
    {
        bool isNew = ChatManager.Instance.IsRecvNewMsg;

        for(int i=0,l=mNewIcon.Length;i<l;i++)
        {
            NGUITools.SetActive(mNewIcon[i],isNew);
        }
    }

    /// <summary>
    /// 채팅 보내기
    /// </summary>
    public void OnSubmit()
    {
        //빈건 안보냄
        if (string.IsNullOrEmpty(mUIInput.value)) return;

        //월드채팅혹은 동맹채팅
        ChatChannelType currChannelType = ChatChannelType.WORLD;
        switch(CurrTap)
        {
            case TapType.ALLIANCE: currChannelType = ChatChannelType.ALLIANCE; break;
            case TapType.TOTAL: currChannelType = ChatChannelType.WORLD; break;
        }

        //보내기
        ChatNetworkManager.Instance.AddSendList(new SendChatPacket(currChannelType, mUIInput.value));
        
        //보낸내용 지우기
        mUIInput.value = string.Empty;
    }

    /// <summary>
    /// 차단유저 변경사항 있을때 호출되는 함수
    /// </summary>
    public void OnUpdateBlockUser()
    {
        ChatAllRefresh(false);

    }

    /// <summary>
    /// 한프레임 쉬고 작동하기
    /// </summary>
    IEnumerator Util_RunNextFrame(params System.Action[] a)
    {
        for(int i=0,l=a.Length;i<l;i++)
        {
            yield return null;
            a[i]();
        }
    }
#endif
}