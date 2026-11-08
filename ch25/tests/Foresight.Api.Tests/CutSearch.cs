namespace Foresight.Api.Tests;

/// <summary>
/// The attack of chapter 24: a binary search for the number at which an
/// answer turns from no to yes.
/// </summary>
public static class CutSearch
{
    /// <summary>
    /// The lowest number from 0 to 100 for which the answer is yes, or
    /// null if it never is, and how many questions that took. It works for
    /// any answer that turns from no to yes once, at one number.
    /// </summary>
    public static async Task<(int? Cut, int Questions)> FindAsync(
        Func<int, Task<bool>> answersYes)
    {
        // The cut is somewhere from low to high; 101 stands for "never".
        int low = 0, high = 101, questions = 0;
        while (low < high)
        {
            var middle = (low + high) / 2;
            questions++;
            if (await answersYes(middle))
                high = middle;      // the cut is here or below
            else
                low = middle + 1;   // the cut is above
        }
        return (low == 101 ? null : low, questions);
    }
}
