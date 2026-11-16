using StudyPilot.Api.Services;

namespace StudyPilot.Api.Tests;

public class HebrewPdfTextFixerTests
{
  [Fact]
  public void FixLine_ReversedHebrew_IsFixed()
  {
    var result = HebrewPdfTextFixer.FixLine("םיגירח חוד תקפה רשפאל תכרעמה לע");

    Assert.Equal("על המערכת לאפשר הפקת דוח חריגים", result);
  }

  [Fact]
  public void FixLine_CorrectHebrew_IsNotChanged()
  {
    const string line = "על המערכת לאפשר הפקת דוח חריגים";

    var result = HebrewPdfTextFixer.FixLine(line);

    Assert.Equal(line, result);
  }

  [Fact]
  public void FixLine_ReversedHebrewWithEnglish_KeepsEnglishReadable()
  {
    var result = HebrewPdfTextFixer.FixLine("םיפסונ םיתרשל Raspberry Pi לע הלעפה");

    Assert.Equal("הפעלה על Raspberry Pi לשרתים נוספים", result);
  }

  [Fact]
  public void FixLine_MisencodedNun_IsReplaced()
  {
    var result = HebrewPdfTextFixer.FixLine("דבלב הייקשªה לש םיימיªפה םיªותªה");

    Assert.Equal("הנתונים הפנימיים של הנשקייה בלבד", result);
  }

  [Fact]
  public void FixLine_LineWithoutFinalLetters_UsesDocumentDirection()
  {
    var result = HebrewPdfTextFixer.FixLine(":תואמגוד", documentLooksReversed: true);

    Assert.Equal("דוגמאות:", result);
  }

  [Fact]
  public void FixLine_ReversedLineWithBrackets_MirrorsBrackets()
  {
    var result = HebrewPdfTextFixer.FixLine("(הנפצה) החטבא", documentLooksReversed: true);

    Assert.Equal("אבטחה (הצפנה)", result);
  }

  [Fact]
  public void FixLine_EnglishOnly_IsNotChanged()
  {
    var result = HebrewPdfTextFixer.FixLine("Hello World 123");

    Assert.Equal("Hello World 123", result);
  }

  [Fact]
  public void Fix_LineRepeatedOnEveryPage_IsRemoved()
  {
    var pages = new List<List<string>>
        {
            new() { "קורס: מבני נתונים 1", "מחסנית היא מבנה נתונים" },
            new() { "קורס: מבני נתונים 2", "תור הוא מבנה נתונים" },
            new() { "קורס: מבני נתונים 3", "גרף הוא אוסף של קודקודים" }
        };

    var result = HebrewPdfTextFixer.Fix(pages);

    Assert.All(result, page => Assert.DoesNotContain(page, line => line.StartsWith("קורס:")));
    Assert.Equal("מחסנית היא מבנה נתונים", result[0].Single());
  }

  [Fact]
  public void Fix_FewerThanThreePages_KeepsAllLines()
  {
    var pages = new List<List<string>>
        {
            new() { "כותרת", "שורה ראשונה" },
            new() { "כותרת", "שורה שנייה" }
        };

    var result = HebrewPdfTextFixer.Fix(pages);

    Assert.Equal(2, result[0].Count);
    Assert.Equal(2, result[1].Count);
  }
}