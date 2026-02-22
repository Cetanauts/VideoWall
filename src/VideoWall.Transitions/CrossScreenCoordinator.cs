using VideoWall.Core.Enums;
using VideoWall.Core.Models;

namespace VideoWall.Transitions;

/// <summary>
/// Coordinates transitions across multiple screens in a screen set
/// Calculates timing offsets for cross-screen wipe effects
/// </summary>
public class CrossScreenCoordinator
{
    /// <summary>
    /// Create a coordinated transition plan for a screen set
    /// </summary>
    /// <param name="screenSet">The screen set configuration</param>
    /// <param name="crossTransition">The cross-screen transition type</param>
    /// <param name="singleTransition">The transition to use on each individual screen</param>
    /// <param name="totalDurationMs">Total duration for the cross-screen transition</param>
    /// <returns>List of timed transition commands for each screen</returns>
    public List<ScreenTransitionCommand> CreateTransitionPlan(
        ScreenSet screenSet,
        CrossScreenTransitionType crossTransition,
        TransitionType singleTransition,
        int totalDurationMs)
    {
        var commands = new List<ScreenTransitionCommand>();

        if (screenSet.Screens.Count == 0) return commands;

        // Get screens in the correct order for this transition
        var orderedScreens = GetOrderedScreens(screenSet, crossTransition);

        // Calculate timing for each screen
        int screenCount = orderedScreens.Count;

        // For cross-wipes, the transition "travels" across screens
        // Each screen starts its transition when the wipe reaches it
        int transitionOverlapMs = CalculateOverlap(crossTransition, totalDurationMs, screenCount);
        int singleScreenDurationMs = CalculateSingleScreenDuration(crossTransition, totalDurationMs, screenCount);

        int currentDelayMs = 0;

        foreach (var screen in orderedScreens)
        {
            commands.Add(new ScreenTransitionCommand
            {
                OutputId = screen.OutputId,
                ScreenPosition = screen,
                TransitionType = singleTransition,
                DelayMs = currentDelayMs,
                DurationMs = singleScreenDurationMs,
                Column = screen.Column,
                Row = screen.Row
            });

            // Calculate delay for next screen
            currentDelayMs = CalculateNextDelay(crossTransition, currentDelayMs, totalDurationMs, screenCount, screen, screenSet);
        }

        return commands;
    }

    /// <summary>
    /// Get screens in order based on transition type
    /// </summary>
    private List<ScreenPosition> GetOrderedScreens(ScreenSet screenSet, CrossScreenTransitionType transition)
    {
        return transition switch
        {
            CrossScreenTransitionType.CrossWipeRight or
            CrossScreenTransitionType.CrossPushRight or
            CrossScreenTransitionType.SequentialLeftToRight =>
                screenSet.Screens.OrderBy(s => s.Column).ThenBy(s => s.Row).ToList(),

            CrossScreenTransitionType.CrossWipeLeft or
            CrossScreenTransitionType.CrossPushLeft or
            CrossScreenTransitionType.SequentialRightToLeft =>
                screenSet.Screens.OrderByDescending(s => s.Column).ThenBy(s => s.Row).ToList(),

            CrossScreenTransitionType.CrossWipeDown or
            CrossScreenTransitionType.CrossPushDown or
            CrossScreenTransitionType.SequentialTopToBottom =>
                screenSet.Screens.OrderBy(s => s.Row).ThenBy(s => s.Column).ToList(),

            CrossScreenTransitionType.CrossWipeUp or
            CrossScreenTransitionType.CrossPushUp or
            CrossScreenTransitionType.SequentialBottomToTop =>
                screenSet.Screens.OrderByDescending(s => s.Row).ThenBy(s => s.Column).ToList(),

            CrossScreenTransitionType.CrossWipeDiagonalBR or
            CrossScreenTransitionType.CascadeTL =>
                screenSet.Screens.OrderBy(s => s.Column + s.Row).ThenBy(s => s.Column).ToList(),

            CrossScreenTransitionType.CrossWipeDiagonalTL or
            CrossScreenTransitionType.CascadeBR =>
                screenSet.Screens.OrderByDescending(s => s.Column + s.Row).ThenBy(s => s.Column).ToList(),

            CrossScreenTransitionType.CrossWipeDiagonalTR or
            CrossScreenTransitionType.CascadeBL =>
                screenSet.Screens.OrderBy(s => s.Column - s.Row).ThenBy(s => s.Column).ToList(),

            CrossScreenTransitionType.CrossWipeDiagonalBL or
            CrossScreenTransitionType.CascadeTR =>
                screenSet.Screens.OrderByDescending(s => s.Column - s.Row).ThenBy(s => s.Column).ToList(),

            CrossScreenTransitionType.Explosion =>
                screenSet.GetScreensByDistanceFromCenter(ascending: true).ToList(),

            CrossScreenTransitionType.Implosion =>
                screenSet.GetScreensByDistanceFromCenter(ascending: false).ToList(),

            CrossScreenTransitionType.SequentialSpiral =>
                GetSpiralOrder(screenSet),

            CrossScreenTransitionType.SequentialRandom =>
                screenSet.Screens.OrderBy(_ => Random.Shared.Next()).ToList(),

            CrossScreenTransitionType.AllAtOnce or
            CrossScreenTransitionType.Independent =>
                screenSet.Screens.ToList(),

            _ => screenSet.Screens.ToList()
        };
    }

    /// <summary>
    /// Get screens in spiral order (outside in)
    /// </summary>
    private List<ScreenPosition> GetSpiralOrder(ScreenSet screenSet)
    {
        var result = new List<ScreenPosition>();
        var remaining = screenSet.Screens.ToList();

        int top = 0, bottom = screenSet.Rows - 1;
        int left = 0, right = screenSet.Columns - 1;

        while (remaining.Count > 0 && top <= bottom && left <= right)
        {
            // Top row
            for (int c = left; c <= right; c++)
            {
                var screen = remaining.FirstOrDefault(s => s.Row == top && s.Column == c);
                if (screen != null)
                {
                    result.Add(screen);
                    remaining.Remove(screen);
                }
            }
            top++;

            // Right column
            for (int r = top; r <= bottom; r++)
            {
                var screen = remaining.FirstOrDefault(s => s.Row == r && s.Column == right);
                if (screen != null)
                {
                    result.Add(screen);
                    remaining.Remove(screen);
                }
            }
            right--;

            // Bottom row
            for (int c = right; c >= left; c--)
            {
                var screen = remaining.FirstOrDefault(s => s.Row == bottom && s.Column == c);
                if (screen != null)
                {
                    result.Add(screen);
                    remaining.Remove(screen);
                }
            }
            bottom--;

            // Left column
            for (int r = bottom; r >= top; r--)
            {
                var screen = remaining.FirstOrDefault(s => s.Row == r && s.Column == left);
                if (screen != null)
                {
                    result.Add(screen);
                    remaining.Remove(screen);
                }
            }
            left++;
        }

        return result;
    }

    /// <summary>
    /// Calculate overlap time for transitions
    /// </summary>
    private int CalculateOverlap(CrossScreenTransitionType transition, int totalDurationMs, int screenCount)
    {
        // For smooth wipes, transitions overlap
        return transition switch
        {
            CrossScreenTransitionType.AllAtOnce => totalDurationMs,
            CrossScreenTransitionType.Independent => 0,
            _ => totalDurationMs / Math.Max(screenCount * 2, 1)
        };
    }

    /// <summary>
    /// Calculate duration for single screen transition
    /// </summary>
    private int CalculateSingleScreenDuration(CrossScreenTransitionType transition, int totalDurationMs, int screenCount)
    {
        return transition switch
        {
            CrossScreenTransitionType.AllAtOnce => totalDurationMs,
            CrossScreenTransitionType.Independent => totalDurationMs,
            // For traveling wipes, each screen gets a portion plus overlap
            _ => Math.Max(totalDurationMs / screenCount + totalDurationMs / (screenCount * 2), 200)
        };
    }

    /// <summary>
    /// Calculate delay for next screen in sequence
    /// </summary>
    private int CalculateNextDelay(
        CrossScreenTransitionType transition,
        int currentDelay,
        int totalDurationMs,
        int screenCount,
        ScreenPosition currentScreen,
        ScreenSet screenSet)
    {
        if (transition == CrossScreenTransitionType.AllAtOnce)
        {
            return 0; // All start at same time
        }

        if (transition == CrossScreenTransitionType.Independent)
        {
            return 0; // All start at same time but independently
        }

        // Calculate step delay based on transition type
        int stepDelay = totalDurationMs / Math.Max(screenCount, 1);

        // For wave effects, use sine-based delays
        if (transition == CrossScreenTransitionType.WaveHorizontal ||
            transition == CrossScreenTransitionType.WaveVertical)
        {
            double phase = transition == CrossScreenTransitionType.WaveHorizontal
                ? (double)currentScreen.Column / screenSet.Columns
                : (double)currentScreen.Row / screenSet.Rows;

            int waveDelay = (int)(Math.Sin(phase * Math.PI) * stepDelay);
            return currentDelay + stepDelay - waveDelay;
        }

        return currentDelay + stepDelay;
    }

    /// <summary>
    /// Create a synchronized transition that makes content appear to flow across screens
    /// </summary>
    public List<ScreenTransitionCommand> CreateFlowingTransition(
        ScreenSet screenSet,
        WipeDirection direction,
        int totalDurationMs)
    {
        var commands = new List<ScreenTransitionCommand>();

        // For a flowing wipe, we need to calculate pixel-level timing
        // so the wipe edge moves smoothly across all screens

        double totalWidth = screenSet.TotalWidth;
        double totalHeight = screenSet.TotalHeight;
        double pixelsPerMs = direction switch
        {
            WipeDirection.Left or WipeDirection.Right => totalWidth / totalDurationMs,
            WipeDirection.Up or WipeDirection.Down => totalHeight / totalDurationMs,
            _ => totalWidth / totalDurationMs
        };

        foreach (var screen in screenSet.Screens)
        {
            // Calculate when the wipe edge reaches this screen
            double screenStartPixel = direction switch
            {
                WipeDirection.Right => screen.Column * screenSet.ScreenWidth,
                WipeDirection.Left => (screenSet.Columns - 1 - screen.Column) * screenSet.ScreenWidth,
                WipeDirection.Down => screen.Row * screenSet.ScreenHeight,
                WipeDirection.Up => (screenSet.Rows - 1 - screen.Row) * screenSet.ScreenHeight,
                _ => 0
            };

            double screenEndPixel = screenStartPixel + (direction switch
            {
                WipeDirection.Left or WipeDirection.Right => screenSet.ScreenWidth,
                WipeDirection.Up or WipeDirection.Down => screenSet.ScreenHeight,
                _ => screenSet.ScreenWidth
            });

            int startDelayMs = (int)(screenStartPixel / pixelsPerMs);
            int screenDurationMs = (int)((screenEndPixel - screenStartPixel) / pixelsPerMs);

            var singleTransition = direction switch
            {
                WipeDirection.Left => TransitionType.WipeLeft,
                WipeDirection.Right => TransitionType.WipeRight,
                WipeDirection.Up => TransitionType.WipeUp,
                WipeDirection.Down => TransitionType.WipeDown,
                _ => TransitionType.WipeRight
            };

            commands.Add(new ScreenTransitionCommand
            {
                OutputId = screen.OutputId,
                ScreenPosition = screen,
                TransitionType = singleTransition,
                DelayMs = startDelayMs,
                DurationMs = screenDurationMs,
                Column = screen.Column,
                Row = screen.Row
            });
        }

        return commands;
    }
}

/// <summary>
/// A command to execute a transition on a specific screen
/// </summary>
public class ScreenTransitionCommand
{
    /// <summary>
    /// Target output ID
    /// </summary>
    public string OutputId { get; set; } = string.Empty;

    /// <summary>
    /// Screen position info
    /// </summary>
    public ScreenPosition? ScreenPosition { get; set; }

    /// <summary>
    /// Transition type to execute
    /// </summary>
    public TransitionType TransitionType { get; set; }

    /// <summary>
    /// Delay before starting transition (milliseconds)
    /// </summary>
    public int DelayMs { get; set; }

    /// <summary>
    /// Duration of the transition (milliseconds)
    /// </summary>
    public int DurationMs { get; set; }

    /// <summary>
    /// Column position (for reference)
    /// </summary>
    public int Column { get; set; }

    /// <summary>
    /// Row position (for reference)
    /// </summary>
    public int Row { get; set; }

    /// <summary>
    /// Scheduled execution time (set by coordinator)
    /// </summary>
    public DateTime? ScheduledTime { get; set; }
}
