using System.Text;

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
        { 'b', new[] { 'ب', 'ب', 'ب', 'ب' } }, // Placeholder for mapping logic if needed, but using direct chars below
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
        { 'k', new[] { 'ك', 'ك', 'ك', 'ك' } },
        { 'ك', new[] { 'ﻙ', 'ﻛ', 'ﻜ', 'ﻚ' } },
        { 'l', new[] { 'ل', 'ل', 'ل', 'ل' } },
        { 'ل', new[] { 'ﻝ', 'ﻟ', 'ﻠ', 'ﻞ' } },
        { 'm', new[] { 'م', 'م', 'م', 'م' } },
        { 'م', new[] { 'ﻡ', 'ﻣ', 'ﻤ', 'ﻢ' } },
        { 'n', new[] { 'ن', 'ن', 'ن', 'ن' } },
        { 'ن', new[] { 'ﻥ', 'ﻧ', 'ﻨ', 'ﻦ' } },
        { 'ه', new[] { 'ﻩ', 'ﻫ', 'ﻬ', 'ﻪ' } },
        { 'و', new[] { 'ﻭ', 'ﻭ', 'ﻭ', 'ﻭ' } },
        { 'y', new[] { 'ي', 'ي', 'ي', 'ي' } },
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

        // Simple check: if no Arabic characters, return as is (fixes English text reversal)
        if (!input.Any(c => c >= 0x0600 && c <= 0x06FF))
            return input;

        var sb = new StringBuilder();
        // Step 1: Handle Lam-Alef (Special Case)
        input = HandleLamAlef(input);

        // Step 2: Shape characters
        for (int i = 0; i < input.Length; i++)
        {
            char current = input[i];
            
            if (!ArabicMap.ContainsKey(current))
            {
                sb.Append(current);
                continue;
            }

            // Check previous and next to determine shape
            bool prevConnects = i > 0 && Connects(input[i - 1]) && !NonConnectors.Contains(input[i - 1]);
            bool nextConnects = i < input.Length - 1 && Connects(input[i + 1]);

            if (prevConnects && nextConnects)
                sb.Append(ArabicMap[current][2]); // Medial
            else if (prevConnects && !nextConnects)
                sb.Append(ArabicMap[current][3]); // Final
            else if (!prevConnects && nextConnects && !NonConnectors.Contains(current))
                sb.Append(ArabicMap[current][1]); // Initial
            else
                sb.Append(ArabicMap[current][0]); // Isolated
        }

        // Step 3: Reverse for Bidi LTR rendering (standard PDF behavior without Bidi engine)
        char[] result = sb.ToString().ToCharArray();
        Array.Reverse(result);
        return new string(result);
    }

    private static bool Connects(char c)
    {
        return ArabicMap.ContainsKey(c);
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
