using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SHOW BALL 진행 데이터를 프로젝트 공용 FlatBuffers 세이브(Master.dat)에 연결한다.
///
/// 데이터 정의: Assets/FlatBuffers/UserData.fbs 의 showballdata (table ShowBallData)
/// 생성 코드:   BanpoFri/FlatBuffersConvertor(F12)가 만든 UserDataClass/ShowBallData.cs (UserDataSystem.Showballdata)
///
/// BananaBall 어셈블리(asmdef)는 Assembly-CSharp의 UserDataSystem을 직접 참조할 수 없어서,
/// 이 파일(Assembly-CSharp)이 BananaBall.Save.Backend 에 자신을 등록한다.
/// GameRoot가 떠 있으면 그 UserData를 함께 쓰고, SHOW BALL 단독 씬에서는 UserDataSystem을 직접 만들어 Master.dat을 읽고 쓴다.
/// </summary>
public sealed class ShowBallSaveBridge : BananaBall.ISaveBackend
{
    UserDataSystem own;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() => BananaBall.Save.Backend = new ShowBallSaveBridge();

#if UNITY_EDITOR
    // Edit mode as well (menu BananaBall > Reset Save Data).
    [UnityEditor.InitializeOnLoadMethod]
    static void RegisterInEditor()
    {
        if (BananaBall.Save.Backend == null) Register();
    }
#endif

    UserDataSystem Data
    {
        get
        {
            // GameRoot.Instance would create a GameRoot object when there is none; GetInstance only looks.
            var root = GameRoot.GetInstance();
            if (root != null && root.UserData != null && root.UserData.DataState != DataState.None) return root.UserData;
            if (own == null)
            {
                own = new UserDataSystem();
                own.Load();
                // TimeSystem is only started by GameRoot. Without it a new save gets year 1 as its first-start time
                // (Gamestarttime), which the framework's "days since first start" checks would misread.
                if (own.FirstStartTime.Year < 2000) own.FirstStartTime = System.DateTime.Now;
            }
            return own;
        }
    }

    ShowBallData D => Data.Showballdata;

    public List<int> Lineup => D.Lineup;
    public int Fans { get => D.Fans; set => D.Fans = value; }
    public int Matches { get => D.Matches; set => D.Matches = value; }
    public int LastVisitor { get => D.Lastvisitor; set => D.Lastvisitor = value; }
    public List<string> Hints => D.Hints;
    public List<string> MatchLog => D.Matchlog;

    public void Commit() => Data.Save(true);

    public void Clear()
    {
        // Outside play mode another session may have written Master.dat since we read it: start from the file.
        if (!Application.isPlaying) own = null;
        var d = D;
        d.Lineup.Clear();
        d.Fans = 0;
        d.Matches = 0;
        d.Lastvisitor = 0;
        d.Hints.Clear();
        d.Matchlog.Clear();
        Commit();
    }
}
