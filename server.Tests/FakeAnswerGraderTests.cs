using StudyPilot.Api.Services.Ai;

namespace StudyPilot.Api.Tests;

public class FakeAnswerGraderTests
{
  private const string Question = "מהו עיקרון הפעולה של תור?";
  private const string ModelAnswer = "תור פועל לפי העיקרון ראשון נכנס, ראשון יוצא.";

  private readonly FakeAnswerGrader _grader = new();

  [Fact]
  public async Task GradeOpenAnswers_EmptyAnswer_GetsZero()
  {
    // Arrange
    var answers = new List<OpenAnswerToGrade>
        {
            new(Question, ModelAnswer, "   ")
        };

    // Act
    var grades = await _grader.GradeOpenAnswersAsync(answers);

    // Assert
    Assert.Equal(0, grades[0].Score);
  }

  [Fact]
  public async Task GradeOpenAnswers_AnswerWithKeyWords_GetsFullScore()
  {
    var answers = new List<OpenAnswerToGrade>
        {
            new(Question, ModelAnswer, "תור עובד לפי ראשון נכנס ראשון יוצא")
        };

    var grades = await _grader.GradeOpenAnswersAsync(answers);

    Assert.Equal(1, grades[0].Score);
  }

  [Fact]
  public async Task GradeOpenAnswers_UnrelatedAnswer_GetsZero()
  {
    var answers = new List<OpenAnswerToGrade>
        {
            new(Question, ModelAnswer, "אין לי מושג")
        };

    var grades = await _grader.GradeOpenAnswersAsync(answers);

    Assert.Equal(0, grades[0].Score);
  }

  [Fact]
  public async Task GradeOpenAnswers_SeveralAnswers_ReturnsOneGradePerAnswerInOrder()
  {
    var answers = new List<OpenAnswerToGrade>
        {
            new(Question, ModelAnswer, ""),
            new(Question, ModelAnswer, "תור עובד לפי ראשון נכנס ראשון יוצא"),
            new(Question, ModelAnswer, "אין לי מושג")
        };

    var grades = await _grader.GradeOpenAnswersAsync(answers);

    Assert.Equal(3, grades.Count);
    Assert.Equal(0, grades[0].Score);
    Assert.Equal(1, grades[1].Score);
    Assert.Equal(0, grades[2].Score);
  }

  [Fact]
  public async Task GradeOpenAnswers_AnyAnswer_ScoreIsBetweenZeroAndOne()
  {
    var answers = new List<OpenAnswerToGrade>
        {
            new(Question, ModelAnswer, ModelAnswer + " " + ModelAnswer + " ועוד מילים רבות")
        };

    var grades = await _grader.GradeOpenAnswersAsync(answers);

    Assert.InRange(grades[0].Score, 0, 1);
  }
}