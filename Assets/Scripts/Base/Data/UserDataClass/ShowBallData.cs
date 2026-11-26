using System;
using System.Collections.Generic;
using UniRx;
using Google.FlatBuffers;

public partial class UserDataSystem
{
    public ShowBallData Showballdata { get; private set; } = new ShowBallData();



    private void SaveData_ShowBallData(FlatBufferBuilder builder)
    {
        // 선언된 변수들은 모두 저장되어야함

        // Showballdata 단일 저장
        // Showballdata.Lineup 처리 GenerateItemSaveCode Array
        int[] showballdata_lineup_Array = null;
        VectorOffset showballdata_lineup_Vector = default;

        if(Showballdata.Lineup.Count > 0){
            showballdata_lineup_Array = new int[Showballdata.Lineup.Count];
            int showballdata_lineup_idx = 0;
            foreach(int showballdata_lineup_val in Showballdata.Lineup){
                showballdata_lineup_Array[showballdata_lineup_idx++] = showballdata_lineup_val;
            }
            showballdata_lineup_Vector = BanpoFri.Data.ShowBallData.CreateLineupVector(builder, showballdata_lineup_Array);
        }

        // Showballdata.Hints 처리 GenerateItemSaveCode Array
        StringOffset[] showballdata_hints_Array = null;
        VectorOffset showballdata_hints_Vector = default;

        if(Showballdata.Hints.Count > 0){
            showballdata_hints_Array = new StringOffset[Showballdata.Hints.Count];
            int showballdata_hints_idx = 0;
            foreach(string showballdata_hints_str in Showballdata.Hints){
                showballdata_hints_Array[showballdata_hints_idx++] = builder.CreateString(showballdata_hints_str);
            }
            showballdata_hints_Vector = BanpoFri.Data.ShowBallData.CreateHintsVector(builder, showballdata_hints_Array);
        }

        // Showballdata.Matchlog 처리 GenerateItemSaveCode Array
        StringOffset[] showballdata_matchlog_Array = null;
        VectorOffset showballdata_matchlog_Vector = default;

        if(Showballdata.Matchlog.Count > 0){
            showballdata_matchlog_Array = new StringOffset[Showballdata.Matchlog.Count];
            int showballdata_matchlog_idx = 0;
            foreach(string showballdata_matchlog_str in Showballdata.Matchlog){
                showballdata_matchlog_Array[showballdata_matchlog_idx++] = builder.CreateString(showballdata_matchlog_str);
            }
            showballdata_matchlog_Vector = BanpoFri.Data.ShowBallData.CreateMatchlogVector(builder, showballdata_matchlog_Array);
        }

        // Showballdata 최종 생성 및 추가
        var showballdata_Offset = BanpoFri.Data.ShowBallData.CreateShowBallData(
            builder,
            showballdata_lineup_Vector,
            Showballdata.Fans,
            Showballdata.Matches,
            Showballdata.Lastvisitor,
            showballdata_hints_Vector,
            showballdata_matchlog_Vector
        );


        Action cbAddDatas = () => {
            BanpoFri.Data.UserData.AddShowballdata(builder, showballdata_Offset);
        };

        cb_SaveAddDatas += cbAddDatas;

    }
    private void LoadData_ShowBallData()
    {
        // 로드 함수 내용

        // Showballdata 로드
        var fb_Showballdata = flatBufferUserData.Showballdata;
        if (fb_Showballdata.HasValue)
        {
            Showballdata.Fans = fb_Showballdata.Value.Fans;
            Showballdata.Matches = fb_Showballdata.Value.Matches;
            Showballdata.Lastvisitor = fb_Showballdata.Value.Lastvisitor;

            // Lineup 리스트 로드
            Showballdata.Lineup.Clear();
            for (int j = 0; j < fb_Showballdata.Value.LineupLength; j++)
            {
                int lineup_val = fb_Showballdata.Value.Lineup(j);
                Showballdata.Lineup.Add(lineup_val);
            }

            // Hints 리스트 로드
            Showballdata.Hints.Clear();
            for (int j = 0; j < fb_Showballdata.Value.HintsLength; j++)
            {
                string hints_val = fb_Showballdata.Value.Hints(j);
                Showballdata.Hints.Add(hints_val);
            }

            // Matchlog 리스트 로드
            Showballdata.Matchlog.Clear();
            for (int j = 0; j < fb_Showballdata.Value.MatchlogLength; j++)
            {
                string matchlog_val = fb_Showballdata.Value.Matchlog(j);
                Showballdata.Matchlog.Add(matchlog_val);
            }
        }
    }

}

public class ShowBallData
{
    public List<int> Lineup = new List<int>();
    public int Fans { get; set; } = 0;
    public int Matches { get; set; } = 0;
    public int Lastvisitor { get; set; } = 0;
    public List<string> Hints = new List<string>();
    public List<string> Matchlog = new List<string>();

}
