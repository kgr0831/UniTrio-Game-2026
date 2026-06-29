using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CSV 파일을 파싱하여 런타임 DialogueSO를 생성하고 DialogueDatabase에 등록합니다.
///
/// CSV 컬럼 형식 (첫 줄 = 헤더, 무시됨):
///   dialogue_id, speaker, text, choice1_text, choice1_next, choice2_text, choice2_next, choice3_text, choice3_next
///
/// 규칙:
///   - 같은 dialogue_id를 가진 행들 → 하나의 DialogueSO (여러 줄 대화)
///   - 선택지(choice)는 마지막 줄에만 의미 있음 (여러 줄 중 선택지가 있는 줄 우선)
///   - speaker 열 = CharacterSO의 CharacterName과 일치해야 함
///   - 빈 줄·#으로 시작하는 줄은 무시 (주석)
/// </summary>
public static class DialogueCSVLoader
{
    /// <summary>
    /// TextAsset CSV를 파싱하여 생성된 DialogueSO 딕셔너리를 반환합니다.
    /// </summary>
    /// <param name="csv">Resources에서 로드한 CSV TextAsset</param>
    /// <param name="characters">CharacterSO 배열 (speaker 이름 매핑용)</param>
    public static Dictionary<string, DialogueSO> Load(TextAsset csv, CharacterSO[] characters)
    {
        var result = new Dictionary<string, DialogueSO>();
        if (csv == null) { Debug.LogWarning("[DialogueCSVLoader] CSV asset이 null입니다."); return result; }

        // CharacterName → SO 빠른 조회
        var charMap = BuildCharacterMap(characters);

        // dialogue_id → 줄 목록 (파싱 순서 유지)
        var linesByDialogue = new Dictionary<string, List<ParsedLine>>();
        var orderList = new List<string>(); // 중복 없는 순서 추적

        string[] rows = csv.text.Split('\n');
        for (int i = 1; i < rows.Length; i++) // 헤더(row 0) 건너뜀
        {
            string row = rows[i].Trim();
            if (string.IsNullOrEmpty(row) || row.StartsWith("#")) continue;

            string[] cols = SplitCSVRow(row);
            if (cols.Length < 3) continue;

            string dialogueId = cols[0].Trim();
            if (string.IsNullOrEmpty(dialogueId)) continue;

            if (!linesByDialogue.ContainsKey(dialogueId))
            {
                linesByDialogue[dialogueId] = new List<ParsedLine>();
                orderList.Add(dialogueId);
            }

            linesByDialogue[dialogueId].Add(ParseRow(cols, charMap));
        }

        // DialogueSO 생성
        foreach (string id in orderList)
        {
            var parsed = linesByDialogue[id];
            var so = ScriptableObject.CreateInstance<DialogueSO>();
            so.name = id;

            var lines = new DialogueLine[parsed.Count];
            for (int j = 0; j < parsed.Count; j++)
                lines[j] = ToDialogueLine(parsed[j], charMap);

            so.Lines = lines;
            result[id] = so;
        }

        return result;
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────

    private static Dictionary<string, CharacterSO> BuildCharacterMap(CharacterSO[] characters)
    {
        var map = new Dictionary<string, CharacterSO>();
        if (characters == null) return map;
        foreach (var c in characters)
            if (c != null && !string.IsNullOrEmpty(c.CharacterName))
                map[c.CharacterName] = c;
        return map;
    }

    private static ParsedLine ParseRow(string[] cols, Dictionary<string, CharacterSO> charMap)
    {
        // cols: [0]=id, [1]=speaker, [2]=text, [3]=c1text, [4]=c1next, [5]=c2text, [6]=c2next, [7]=c3text, [8]=c3next
        var pl = new ParsedLine
        {
            SpeakerName = cols.Length > 1 ? cols[1].Trim() : string.Empty,
            Text        = cols.Length > 2 ? UnescapeCSV(cols[2]) : string.Empty,
        };

        var choices = new List<DialogueChoice>();
        for (int c = 3; c + 1 < cols.Length; c += 2)
        {
            string choiceText = cols[c].Trim();
            string choiceNext = cols[c + 1].Trim();
            if (!string.IsNullOrEmpty(choiceText))
                choices.Add(new DialogueChoice { Text = choiceText, NextDialogueId = choiceNext });
        }
        pl.Choices = choices.Count > 0 ? choices.ToArray() : null;

        return pl;
    }

    private static DialogueLine ToDialogueLine(ParsedLine pl, Dictionary<string, CharacterSO> charMap)
    {
        charMap.TryGetValue(pl.SpeakerName, out CharacterSO speaker);
        return new DialogueLine
        {
            Speaker = speaker,
            Text    = pl.Text,
            Choices = pl.Choices,
        };
    }

    /// <summary>쉼표로 구분된 CSV 행 파싱 (큰따옴표 내 쉼표 허용).</summary>
    private static string[] SplitCSVRow(string row)
    {
        var cols = new List<string>();
        bool inQuote = false;
        int start = 0;

        for (int i = 0; i < row.Length; i++)
        {
            char c = row[i];
            if (c == '"') { inQuote = !inQuote; continue; }
            if (c == ',' && !inQuote)
            {
                cols.Add(row.Substring(start, i - start).Trim('"'));
                start = i + 1;
            }
        }
        cols.Add(row.Substring(start).Trim('"'));
        return cols.ToArray();
    }

    private static string UnescapeCSV(string s)
        => s.Replace("\\n", "\n").Replace("\"\"", "\"").Trim('"');

    private class ParsedLine
    {
        public string SpeakerName;
        public string Text;
        public DialogueChoice[] Choices;
    }
}
