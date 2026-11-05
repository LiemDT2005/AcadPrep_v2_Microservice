using System.Text.RegularExpressions;

namespace Ai.API.Services;

// History turn used by the guardrails (Role: "user" or "model").
public record TutorTurn(string Role, string Content);

// Ported from the read-only FTOEIC reference (AskAiCommandHandler, Huy 2026-11-05): out-of-scope subject
// detection and the answer-leak guard that replaces a reply disclosing a multiple-choice answer with a
// pedagogical hint. Brand renamed to AcadPrep; regular expressions kept unchanged.
public static class TutorGuardrails
{
    private static readonly Regex MultipleChoicePromptRegex = new(
        @"(?:\b[A-D]\s*[.)\:]|\([A-D]\))\s+[A-Za-z0-9]",
        RegexOptions.Compiled);

    private static readonly Regex OptionEntryRegex = new(
        @"(?:^|\s)(?:[A-D]\s*[.)\:]|\([A-D]\))\s*(.+?)(?=(?:\s+(?:[A-D]\s*[.)\:]|\([A-D]\)))|$)",
        RegexOptions.Compiled);

    private static readonly Regex AnswerDisclosureRegex = new(
        @"(?:(?:đáp\s+án\s+(?:đúng|chính\s+xác|cần\s+chọn)?\s*(?:là\b\s*:?|:|\s)\s*)|(?:(?:bạn\s+)?(?:hãy\s+)?chọn\s+(?:đáp\s+án|phương\s+án)?\s*)|(?:(?:the\s+)?correct\s+(?:answer|option)\s*(?:is\b\s*:?|:)?\s*)|(?:choose\s+(?:option\s+)?))[*_`""'(\[]*\b([A-D])\b[*_`""')\]]*",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex DirectConfirmationRegex = new(
        @"\b(?:đáp\s+án\s+của\s+bạn\s+(?:chính\s+xác|đúng\s+rồi|chuẩn\s+rồi)|bạn\s+đã\s+chọn\s+đúng)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex TenseLeakRegex = new(
        @"(?:\b(?:thì|tense)\s+(?:cần\s+(?:dùng|chọn)|đúng|chính\s+xác|thích\s+hợp)?\s*(?:là|:)\s*(?:thì\s+)?|(?:cần\s+(?:dùng|chọn|chia\s+ở)\s+thì\s+)|(?:the\s+correct\s+tense\s+is\s+))[*_`""']*(?:quá\s+khứ\s+(?:hoàn\s+thành(?:\s+tiếp\s+diễn)?|tiếp\s+diễn|đơn)|hiện\s+tại\s+(?:hoàn\s+thành(?:\s+tiếp\s+diễn)?|tiếp\s+diễn|đơn)|tương\s+lai\s+(?:hoàn\s+thành(?:\s+tiếp\s+diễn)?|tiếp\s+diễn|đơn)|past\s+perfect(?:\s+continuous)?|past\s+(?:continuous|simple)|present\s+perfect(?:\s+continuous)?|present\s+(?:continuous|simple))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex DistractorLetterRegex = new(
        @"\bloại\s+(?:bỏ|trừ)?\s*(?:phương\s+án|đáp\s+án)?\s*([A-D])\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex OptionDisclosureClaimRegex = new(
        @"(?:\b(?:đáp\s+án|phương\s+án|từ\s+cần\s+(?:điền|chọn)|câu\s+trả\s+lời|công\s+thức|kết\s+quả)\s+(?:đúng|chính\s+xác|cần\s+chọn)?\s*(?:là|:)\s*|(?:(?:the\s+)?correct\s+(?:answer|word|option|formula)\s*(?:is\b\s*:?|:)?\s*))[*_`""']*([^\r\n.?!]+)[*_`""']*",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ClarificationIntentRegex = new(
        @"(?:\b(?:không\s+phải\s+(?:là\s+)?(?:bài\s+tập|câu\s+hỏi\s+trắc\s+nghiệm|đề\s+thi)|chỉ\s+(?:hỏi|muốn\s+biết|thắc\s+mắc)\s+(?:nghĩa|từ\s+vựng|ngữ\s+pháp|cách\s+dùng)|đây\s+là\s+đoạn\s+văn|không\s+cần\s+(?:chọn\s+đáp\s+án|giải\s+bài))\b)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex LearnerChoiceOrReviewRegex = new(
        @"(?:\b(?:tôi|mình|em|người\s+học|learner|i)\s+(?:chọn|nghĩ|đoán|choose|picked?|selected?)\s+(?:là\s+)?(?:đáp\s+án|phương\s+án)?\s*[*_`""'(\[]*\b([A-D])\b|" +
        @"(?:chọn|phương\s+án|đáp\s+án)?\s*[*_`""'(\[]*\b([A-D])\b[*_`""')\]]*\s*(?:có\s+đúng\s+không|đúng\s+không|phải\s+không|được\s+không|is\s+it\s+correct)\b|" +
        @"\b(?:tại\s+sao|vì\s+sao|why)\b.*?\b(?:chọn|đáp\s+án\s+(?:lại\s+)?là|(?:the\s+)?correct\s+answer\s+is)\s*[*_`""'(\[]*\b([A-D])\b|" +
        @"\b(?:giải\s+thích|explain)\b.*?\b(?:tôi\s+vừa\s+làm\s+sai|đã\s+làm\s+sai|làm\s+sai|đáp\s+án|chọn\s+[A-D])\b)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex OutOfScopeSubjectRegex = new(
        @"(?:\b(?:con\s+lắc\s+lò\s+xo|dao\s+động\s+điều\s+hòa|cơ\s+năng|thấu\s+kính\s+hội\s+tụ|quang\s+điện|phương\s+trình\s+hóa\s+học|khối\s+lượng\s+mol|tính\s+(?:đạo\s+hàm|tích\s+phân|chu\s+kỳ|gia\s+tốc|vận\s+tốc))\b|" +
        @"\bcalculate\s+the\s+(?:period|frequency|oscillation|integral|derivative|kinetic\s+energy)\b|" +
        @"\bspring\s+oscillator\b.*\b(?:energy|period|amplitude)\b)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex EnglishLearningIntentRegex = new(
        @"(?:\b(?:tiếng\s+anh|english|dịch|translate|nghĩa\s+(?:là|của)|từ\s+vựng|vocabulary|ngữ\s+pháp|grammar|phát\s+âm|pronunciation|reading|listening|toeic|passage|collocation|idiom)\b)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PastTimeCueRegex = new(
        @"\b(?:yesterday|ago|last\s+(?:night|week|month|year)|in\s+(?:19\d\d|20\d\d))\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex SequenceTimeCueRegex = new(
        @"\b(?:by\s+the\s+time|before|after|when|while|as\s+soon\s+as)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex FutureTimeCueRegex = new(
        @"\b(?:tomorrow|next\s+(?:week|month|year)|soon|in\s+the\s+future)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PerfectAspectCueRegex = new(
        @"\b(?:since|for\s+\d+|already|yet|so\s+far|up\s+to\s+now)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ModalOrInfinitiveCueRegex = new(
        @"(?:\b(?:can|could|should|would|will|may|might|must)\s+[_]+|\bto\s+[_]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public const string OutOfScopeRedirectVietnamese =
        "AcadPrep AI Tutor là trợ lý chuyên sâu về tiếng Anh và luyện thi TOEIC. Mình không hỗ trợ giải bài tập các môn học khác như Vật lý, Hóa học hay Toán học. Nếu bạn có câu hỏi hoặc bài tập về tiếng Anh và TOEIC, hãy nhắn cho mình nhé!";

    public const string OutOfScopeRedirectEnglish =
        "AcadPrep AI Tutor is a specialized assistant for English learning and TOEIC preparation. I do not solve exercises for other subjects such as Physics, Chemistry, or Mathematics. If you have questions about English vocabulary, grammar, or TOEIC practice, please feel free to ask!";

    public const string DefaultPedagogicalHint =
        "Để tự tìm đáp án chính xác cho câu trắc nghiệm này, bạn có thể áp dụng các bước sau:\n" +
        "- Xác định vị trí chỗ trống: câu đã có chủ ngữ và động từ chính chưa, hay cần một từ loại cụ thể (danh từ, tính từ, trạng từ hay động từ)?\n" +
        "- Quan sát các từ khóa và dấu hiệu ngữ pháp liền trước hoặc liền sau chỗ trống để suy luận.\n" +
        "Nếu bạn muốn tìm hiểu kỹ hơn về ý nghĩa của từ vựng hay quy tắc ngữ pháp nào trong câu, hãy nhắn cho mình nhé!";


    public static bool IsOutOfScopeSubject(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;
        return OutOfScopeSubjectRegex.IsMatch(message) && !EnglishLearningIntentRegex.IsMatch(message);
    }

    private static bool IsEnglishPrompt(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;
        // If there are no Vietnamese specific diacritics, treat as English prompt
        return !Regex.IsMatch(message, @"[àáạảãâầấậẩẫăằắặẳẵèéẹẻẽêềếệểễìíịỉĩòóọỏõôồốộổỗơờớợởỡùúụủũưừứựửữỳýỵỷỹđ]", RegexOptions.IgnoreCase);
    }

    public static bool IsClarificationNotExercise(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;
        return ClarificationIntentRegex.IsMatch(message);
    }

    public static bool IsLearnerCheckingChoiceOrReviewing(string userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage)) return false;
        return LearnerChoiceOrReviewRegex.IsMatch(userMessage);
    }

    public static bool IsAnswerLeak(string userMessage, IReadOnlyList<TutorTurn>? history, string reply)
    {
        if (string.IsNullOrWhiteSpace(reply)) return false;

        // Nếu người dùng đang đính chính/làm rõ không phải bài tập, không áp dụng guard trắc nghiệm
        if (IsClarificationNotExercise(userMessage))
        {
            return false;
        }

        // Section 3C & 3D: Nếu người học đã đưa ra lựa chọn và muốn được kiểm tra, hoặc hỏi để xem lại/giải thích câu đã làm
        if (IsLearnerCheckingChoiceOrReviewing(userMessage))
        {
            return false;
        }

        // Kiểm tra xem tin nhắn hiện tại hoặc lượt hỏi liền trước có chứa câu hỏi trắc nghiệm không
        bool hasMultipleChoiceContext = HasMultipleChoiceOptions(userMessage) ||
            (history != null && history.Where(m => m.Role.Equals("user", StringComparison.OrdinalIgnoreCase)).TakeLast(1).Any(m => HasMultipleChoiceOptions(m.Content)));

        // Nếu người học không đưa ra bài tập trắc nghiệm, các câu trả lời thông thường không bị chặn
        if (!hasMultipleChoiceContext)
        {
            return false;
        }

        // 1. Tiết lộ trực tiếp chữ cái đáp án
        if (AnswerDisclosureRegex.IsMatch(reply)) return true;

        // 2. Xác nhận trực tiếp đúng/sai
        if (DirectConfirmationRegex.IsMatch(reply)) return true;

        // 3. Tiết lộ tên thì độc nhất gián tiếp chỉ ra đáp án
        if (TenseLeakRegex.IsMatch(reply)) return true;

        // 4. Loại trừ 3 phương án để lộ đáp án còn lại
        if (HasDistractorEliminationLeak(reply)) return true;

        // 5. Tiết lộ trực tiếp nội dung phương án đúng hoặc công thức đúng
        if (HasOptionTextDisclosure(userMessage, history, reply)) return true;

        return false;
    }

    private static bool HasDistractorEliminationLeak(string reply)
    {
        var matches = DistractorLetterRegex.Matches(reply);
        if (matches.Count < 3) return false;

        var distinct = matches
            .Select(m => m.Groups[1].Value.ToUpperInvariant())
            .Distinct()
            .Count();

        return distinct >= 3;
    }

    private static bool HasOptionTextDisclosure(string userMessage, IReadOnlyList<TutorTurn>? history, string reply)
    {
        var claimMatch = OptionDisclosureClaimRegex.Match(reply);
        if (!claimMatch.Success) return false;

        string disclosedValue = claimMatch.Groups[1].Value.Trim();
        if (string.IsNullOrWhiteSpace(disclosedValue)) return false;

        // Lấy danh sách options từ câu hỏi hiện tại hoặc câu hỏi trong lịch sử
        var options = ExtractOptionTexts(userMessage);
        if (options.Count < 2 && history != null)
        {
            var lastUserWithMc = history
                .Where(m => m.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
                .TakeLast(1)
                .FirstOrDefault();

            if (lastUserWithMc != null)
            {
                options = ExtractOptionTexts(lastUserWithMc.Content);
            }
        }

        if (options.Count < 2) return false;

        foreach (var opt in options)
        {
            string trimmedOpt = opt.Trim();
            if (trimmedOpt.Length >= 2 &&
                (string.Equals(disclosedValue, trimmedOpt, StringComparison.OrdinalIgnoreCase) ||
                 disclosedValue.StartsWith(trimmedOpt, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static List<string> ExtractOptionTexts(string text)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        var matches = OptionEntryRegex.Matches(text);
        foreach (Match match in matches)
        {
            if (match.Groups[1].Success)
            {
                result.Add(match.Groups[1].Value.Trim());
            }
        }

        return result;
    }

    private static bool HasMultipleChoiceOptions(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var matches = MultipleChoicePromptRegex.Matches(text);
        // Cần ít nhất 2 phương án (ví dụ A và B) để được coi là câu hỏi trắc nghiệm
        return matches.Count >= 2;
    }

    public static string GeneratePedagogicalHint(string userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return DefaultPedagogicalHint;
        }

        // 1. Dấu hiệu thời gian trong quá khứ
        var pastMatch = PastTimeCueRegex.Match(userMessage);
        if (pastMatch.Success)
        {
            return $"Để tự tìm đáp án đúng cho câu này, bạn hãy chú ý đến dấu hiệu thời gian trong quá khứ ('{pastMatch.Value.Trim()}'). Dấu hiệu này chỉ một hành động đã diễn ra và hoàn tất trong quá khứ, vì vậy hãy xác định phương án nào chia động từ ở quá khứ đơn nhé. Nếu cần giải thích thêm về nghĩa của câu hay từ vựng, bạn cứ hỏi mình!";
        }

        // 2. Mệnh đề / liên từ thời gian chỉ trình tự
        var sequenceMatch = SequenceTimeCueRegex.Match(userMessage);
        if (sequenceMatch.Success)
        {
            return $"Để tự tìm đáp án đúng cho câu này, bạn hãy chú ý đến liên từ thời gian ('{sequenceMatch.Value.Trim()}'). Hãy phân tích thứ tự của các hành động (hành động nào xảy ra trước, hành động nào xảy ra sau hoặc kéo dài liên tục) để xác định thì phù hợp cho mệnh đề.";
        }

        // 3. Dấu hiệu thời gian trong tương lai
        var futureMatch = FutureTimeCueRegex.Match(userMessage);
        if (futureMatch.Success)
        {
            return $"Để tự tìm đáp án đúng cho câu này, bạn hãy chú ý đến dấu hiệu thời gian ở tương lai ('{futureMatch.Value.Trim()}'). Dấu hiệu này chỉ một hành động sắp xảy ra, vì vậy hãy đối chiếu phương án diễn tả thì tương lai phù hợp nhé.";
        }

        // 4. Dấu hiệu thì hoàn thành
        var perfectMatch = PerfectAspectCueRegex.Match(userMessage);
        if (perfectMatch.Success)
        {
            return $"Để tự tìm đáp án đúng cho câu này, bạn hãy chú ý đến dấu hiệu ('{perfectMatch.Value.Trim()}'). Dấu hiệu này thường chỉ hành động bắt đầu trong quá khứ và có liên hệ đến hiện tại hoặc một mốc thời gian khác. Hãy đối chiếu các dạng hoàn thành tương ứng nhé.";
        }

        // 5. Cấu trúc sau động từ khuyết thiếu hoặc 'to' nguyên mẫu
        if (ModalOrInfinitiveCueRegex.IsMatch(userMessage))
        {
            return "Để tự giải câu này, hãy chú ý cấu trúc đứng ngay trước chỗ trống. Sau động từ khuyết thiếu hoặc cấu trúc 'to' nguyên mẫu, động từ thường ở dạng nguyên thể. Bạn hãy đối chiếu lại các phương án nhé.";
        }

        // 6. Gợi ý tổng quát về chỗ trống và từ loại
        return DefaultPedagogicalHint;
    }
}
