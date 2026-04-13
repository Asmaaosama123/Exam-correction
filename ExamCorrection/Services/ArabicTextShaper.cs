using System.Text;
using System.Text.RegularExpressions;

namespace ExamCorrection.Services;

public static class ArabicTextShaper
{
    private static readonly Dictionary<char, char[]> ArabicMap = new()
    {
        { 'ء', new[] { 'ء', 'ء', 'ء', 'ء' } },
        { 'آ', new[] { 'آ', 'آ', 'آ', 'آ' } },
        { 'أ', new[] { 'أ', 'أ', 'أ', 'أ' } },
        { 'ؤ', new[] { 'ؤ', 'ؤ', 'ؤ', 'ؤ' } },
        { 'إ', new[] { 'إ', 'إ', 'إ', 'إ' } },
        { 'ئ', new[] { 'ئ', 'ئ', 'ﺌ', 'ﺊ' } },
        { 'ا', new[] { 'ا', 'ا', 'ا', 'ا' } },
        { 'ب', new[] { 'ﺏ', 'ﺑ', 'ﺒ', 'ﺐ' } },
        { 'ت', new[] { 'ﺕ', 'ﺗ', 'ﺘ', 'ﺖ' } },
        { 'ث', new[] { 'ﺙ', 'ﺛ', 'ﺜ', 'ﺚ' } },
        { 'ج', new[] { 'ﺝ', 'ﺟ', 'ﺠ', 'ﺞ' } },
        { 'ح', new[] { 'ﺡ', 'ﺣ', 'ﺤ', 'ﺢ' } },
        { 'خ', new[] { 'ﺥ', 'ﺧ', 'ﺨ', 'ﺦ' } },
        { 'د', new[] { 'ﺩ', 'ﺩ', 'ﺩ', 'ﺩ' } },
        { 'ذ', new[] { 'ﺫ', 'ﺫ', 'ﺫ', 'ﺫ' } },
        { 'ر', new[] { 'ﺭ', 'ﺭ', 'ﺭ', 'ﺭ' } },
        { 'ز', new[] { 'ﺯ', 'ﺯ', 'ﺯ', 'ﺯ' } },
        { 'س', new[] { 'ﺱ', 'ﺳ', 'ﺴ', 'ﺲ' } },
        { 'ش', new[] { 'ﺵ', 'ﺷ', 'ﺸ', 'ﺶ' } },
        { 'ص', new[] { 'ﺹ', 'ﺻ', 'ﺼ', 'ﺺ' } },
        { 'ض', new[] { 'ﺽ', 'ﺿ', 'ﻀ', 'ﺾ' } },
        { 'ط', new[] { 'ﻁ', 'ﻃ', 'ﻂ', 'ﻂ' } },
        { 'ظ', new[] { 'ﻅ', 'ﻇ', 'ﻈ', 'ﻆ' } },
        { 'ع', new[] { 'ﻉ', 'ﻋ', 'ﻌ', 'ﻊ' } },
        { 'غ', new[] { 'ﻍ', 'ﻏ', 'ﻐ', 'ﻎ' } },
        { 'ف', new[] { 'ﻑ', 'ﻓ', 'ﻔ', 'ﻒ' } },
        { 'ق', new[] { 'ﻕ', 'ﻗ', 'ﻘ', 'ﻖ' } },
        { 'ك', new[] { 'ﻙ', 'ﻛ', 'ﻜ', 'ﻚ' } },
        { 'ل', new[] { 'ﻝ', 'ﻟ', 'ﻠ', 'ﻞ' } },
        { 'م', new[] { 'ﻡ', 'ﻣ', 'ﻤ', 'ﻢ' } },
        { 'ن', new[] { 'ﻥ', 'ﻧ', 'ﻨ', 'ﻦ' } },
        { 'ه', new[] { 'ﻩ', 'ﻫ', 'ﻬ', 'ﻪ' } },
        { 'و', new[] { 'ﻭ', 'ﻭ', 'ﻭ', 'ﻭ' } },
        { 'ي', new[] { 'ﻱ', 'ﻳ', 'ﻴ', 'ﻲ' } },
        { 'ة', new[] { 'ﺓ', 'ﺓ', 'ﺔ', 'ﺔ' } },
        { 'ى', new[] { 'ﻯ', 'ﻯ', 'ﻰ', 'ﻰ' } },
        { 'ﻻ', new[] { 'ﻻ', 'ﻻ', 'ﻼ', 'ﻼ' } },
        { 'ﻷ', new[] { 'ﻷ', 'ﻷ', 'ﻸ', 'ﻸ' } },
        { 'ﻹ', new[] { 'ﻹ', 'ﻹ', 'ﻺ', 'ﻺ' } },
        { 'ﻵ', new[] { 'ﻵ', 'ﻵ', 'ﻶ', 'ﻶ' } }
    };

    private static readonly HashSet<char> NonConnectors = new() { 'ا', 'إ', 'أ', 'آ', 'د', 'ذ', 'ر', 'ز', 'و', 'ؤ', ' ', '.', '،', '!' };

    public static string Shape(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";

        // If no Arabic characters at all, return as is (prevents reversing purely English names)
        if (!input.Any(c => IsArabic(c)))
            return input;

        // Step 1: Handle Lam-Alef
        input = HandleLamAlef(input);

        // Step 2: Shape individual Arabic characters in logical order
        var sb = new StringBuilder();
        for (int i = 0; i < input.Length; i++)
        {
            char current = input[i];
            
            if (!ArabicMap.ContainsKey(current))
            {
                sb.Append(current);
                continue;
            }

            bool prevConnects = i > 0 && ArabicMap.ContainsKey(input[i - 1]) && !NonConnectors.Contains(input[i - 1]);
            bool nextConnects = i < input.Length - 1 && ArabicMap.ContainsKey(input[i + 1]);

            if (prevConnects && nextConnects)
                sb.Append(ArabicMap[current][2]); // Medial
            else if (prevConnects && !nextConnects)
                sb.Append(ArabicMap[current][3]); // Final
            else if (!prevConnects && nextConnects && !NonConnectors.Contains(current))
                sb.Append(ArabicMap[current][1]); // Initial
            else
                sb.Append(ArabicMap[current][0]); // Isolated
        }

        string shapedText = sb.ToString();

        // Step 3: Directional Run Segmentation
        // We split the string into segments of "Arabic" and "Non-Arabic"
        var visualRuns = new List<string>();
        var currentRun = new StringBuilder();
        bool? currentIsArabic = null;

        foreach (var c in shapedText)
        {
            bool isAr = IsArabic(c);
            bool isLtr = IsLtr(c);
            bool isNeutral = !isAr && !isLtr;

            if (currentIsArabic == null)
            {
                currentIsArabic = isAr;
                currentRun.Append(c);
            }
            else if (isNeutral)
            {
                // Neutral characters (space, symbols) stay with the current direction
                currentRun.Append(c);
            }
            else if (isAr == currentIsArabic.Value)
            {
                // Same direction as current run
                currentRun.Append(c);
            }
            else
            {
                // Direction changed
                string runText = currentRun.ToString();
                visualRuns.Add(currentIsArabic.Value ? ReverseString(runText) : runText);
                
                currentRun.Clear();
                currentRun.Append(c);
                currentIsArabic = isAr;
            }
        }
        
        if (currentRun.Length > 0)
        {
            visualRuns.Add(currentIsArabic!.Value ? ReverseString(currentRun.ToString()) : currentRun.ToString());
        }

        // Step 4: Reverse the order of runs to achieve overall RTL layout in an LTR engine
        visualRuns.Reverse();
        
        return string.Join("", visualRuns);
    }

    private static bool IsArabic(char c)
    {
        return (c >= 0x0600 && c <= 0x06FF) || 
               (c >= 0x0750 && c <= 0x077F) || 
               (c >= 0x08A0 && c <= 0x08FF) || 
               (c >= 0xFB50 && c <= 0xFDFF) || 
               (c >= 0xFE70 && c <= 0xFEFF);
    }

    private static bool IsLtr(char c)
    {
        // English letters and digits (European and Arabic-Indic)
        return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || 
               (c >= '0' && c <= '9') || (c >= 0x0660 && c <= 0x0669);
    }

    private static string ReverseString(string s)
    {
        char[] charArray = s.ToCharArray();
        Array.Reverse(charArray);
        return new string(charArray);
    }

    private static string HandleLamAlef(string input)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < input.Length; i++)
        {
            if (input[i] == 'ل' && i < input.Length - 1)
            {
                if (input[i + 1] == 'ا') { sb.Append('ﻻ'); i++; continue; }
                if (input[i + 1] == 'أ') { sb.Append('ﻷ'); i++; continue; }
                if (input[i + 1] == 'إ') { sb.Append('ﻹ'); i++; continue; }
                if (input[i + 1] == 'آ') { sb.Append('ﻵ'); i++; continue; }
            }
            sb.Append(input[i]);
        }
        return sb.ToString();
    }
}
