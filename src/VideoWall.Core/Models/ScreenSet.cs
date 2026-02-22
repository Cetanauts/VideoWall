using VideoWall.Core.Enums;

namespace VideoWall.Core.Models;

/// <summary>
/// A screen set defines a group of outputs arranged as a video wall
/// Used for coordinated transitions and content spanning multiple screens
/// </summary>
public class ScreenSet
{
    /// <summary>
    /// Unique identifier for this screen set
    /// </summary>
    public string ScreenSetId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// User-friendly name (e.g., "Lobby Video Wall")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Number of columns in the arrangement
    /// </summary>
    public int Columns { get; set; } = 1;

    /// <summary>
    /// Number of rows in the arrangement
    /// </summary>
    public int Rows { get; set; } = 1;

    /// <summary>
    /// Total screens in this set (Columns * Rows)
    /// </summary>
    public int TotalScreens => Columns * Rows;

    /// <summary>
    /// Screen positions mapping output IDs to grid positions
    /// </summary>
    public List<ScreenPosition> Screens { get; set; } = new();

    /// <summary>
    /// Gap between screens in pixels (for bezel compensation)
    /// </summary>
    public int BezelGapHorizontal { get; set; } = 0;

    /// <summary>
    /// Gap between screens in pixels (for bezel compensation)
    /// </summary>
    public int BezelGapVertical { get; set; } = 0;

    /// <summary>
    /// Individual screen width in pixels
    /// </summary>
    public int ScreenWidth { get; set; } = 1920;

    /// <summary>
    /// Individual screen height in pixels
    /// </summary>
    public int ScreenHeight { get; set; } = 1080;

    /// <summary>
    /// Total virtual width of the screen set
    /// </summary>
    public int TotalWidth => (ScreenWidth * Columns) + (BezelGapHorizontal * (Columns - 1));

    /// <summary>
    /// Total virtual height of the screen set
    /// </summary>
    public int TotalHeight => (ScreenHeight * Rows) + (BezelGapVertical * (Rows - 1));

    /// <summary>
    /// Default transition for single-screen effects within this set
    /// </summary>
    public TransitionType DefaultTransition { get; set; } = TransitionType.Fade;

    /// <summary>
    /// Default cross-screen transition for this set
    /// </summary>
    public CrossScreenTransitionType DefaultCrossTransition { get; set; } = CrossScreenTransitionType.CrossWipeRight;

    /// <summary>
    /// Default transition duration in milliseconds
    /// </summary>
    public int DefaultTransitionDurationMs { get; set; } = 1000;

    /// <summary>
    /// Is this screen set currently active/enabled?
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Created timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last modified timestamp
    /// </summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Get the screen at a specific grid position
    /// </summary>
    public ScreenPosition? GetScreenAt(int column, int row)
    {
        return Screens.FirstOrDefault(s => s.Column == column && s.Row == row);
    }

    /// <summary>
    /// Get screens in order for a left-to-right wipe
    /// </summary>
    public IEnumerable<ScreenPosition> GetScreensLeftToRight()
    {
        return Screens.OrderBy(s => s.Column).ThenBy(s => s.Row);
    }

    /// <summary>
    /// Get screens in order for a right-to-left wipe
    /// </summary>
    public IEnumerable<ScreenPosition> GetScreensRightToLeft()
    {
        return Screens.OrderByDescending(s => s.Column).ThenBy(s => s.Row);
    }

    /// <summary>
    /// Get screens in order for a top-to-bottom wipe
    /// </summary>
    public IEnumerable<ScreenPosition> GetScreensTopToBottom()
    {
        return Screens.OrderBy(s => s.Row).ThenBy(s => s.Column);
    }

    /// <summary>
    /// Get screens in order for a bottom-to-top wipe
    /// </summary>
    public IEnumerable<ScreenPosition> GetScreensBottomToTop()
    {
        return Screens.OrderByDescending(s => s.Row).ThenBy(s => s.Column);
    }

    /// <summary>
    /// Get screens ordered by distance from center (for explosion/implosion)
    /// </summary>
    public IEnumerable<ScreenPosition> GetScreensByDistanceFromCenter(bool ascending = true)
    {
        double centerCol = (Columns - 1) / 2.0;
        double centerRow = (Rows - 1) / 2.0;

        var ordered = Screens.OrderBy(s =>
            Math.Sqrt(Math.Pow(s.Column - centerCol, 2) + Math.Pow(s.Row - centerRow, 2)));

        return ascending ? ordered : ordered.Reverse();
    }

    /// <summary>
    /// Calculate delay offset for a screen based on cross-screen transition
    /// </summary>
    /// <param name="screen">The screen position</param>
    /// <param name="transitionType">The cross-screen transition type</param>
    /// <param name="totalDurationMs">Total duration for the cross-screen transition</param>
    /// <returns>Delay in milliseconds before this screen should start its transition</returns>
    public int CalculateTransitionDelay(ScreenPosition screen, CrossScreenTransitionType transitionType, int totalDurationMs)
    {
        if (TotalScreens <= 1) return 0;

        // Time per "step" in the transition
        int stepsNeeded = transitionType switch
        {
            CrossScreenTransitionType.CrossWipeLeft or CrossScreenTransitionType.CrossWipeRight or
            CrossScreenTransitionType.CrossPushLeft or CrossScreenTransitionType.CrossPushRight => Columns,

            CrossScreenTransitionType.CrossWipeUp or CrossScreenTransitionType.CrossWipeDown or
            CrossScreenTransitionType.CrossPushUp or CrossScreenTransitionType.CrossPushDown => Rows,

            CrossScreenTransitionType.CrossWipeDiagonalTL or CrossScreenTransitionType.CrossWipeDiagonalTR or
            CrossScreenTransitionType.CrossWipeDiagonalBL or CrossScreenTransitionType.CrossWipeDiagonalBR => Columns + Rows - 1,

            _ => TotalScreens
        };

        int stepDuration = totalDurationMs / Math.Max(stepsNeeded, 1);

        return transitionType switch
        {
            CrossScreenTransitionType.CrossWipeRight or CrossScreenTransitionType.CrossPushRight =>
                screen.Column * stepDuration,

            CrossScreenTransitionType.CrossWipeLeft or CrossScreenTransitionType.CrossPushLeft =>
                (Columns - 1 - screen.Column) * stepDuration,

            CrossScreenTransitionType.CrossWipeDown or CrossScreenTransitionType.CrossPushDown =>
                screen.Row * stepDuration,

            CrossScreenTransitionType.CrossWipeUp or CrossScreenTransitionType.CrossPushUp =>
                (Rows - 1 - screen.Row) * stepDuration,

            CrossScreenTransitionType.CrossWipeDiagonalBR =>
                (screen.Column + screen.Row) * stepDuration,

            CrossScreenTransitionType.CrossWipeDiagonalTL =>
                ((Columns - 1 - screen.Column) + (Rows - 1 - screen.Row)) * stepDuration,

            CrossScreenTransitionType.CrossWipeDiagonalBL =>
                ((Columns - 1 - screen.Column) + screen.Row) * stepDuration,

            CrossScreenTransitionType.CrossWipeDiagonalTR =>
                (screen.Column + (Rows - 1 - screen.Row)) * stepDuration,

            CrossScreenTransitionType.AllAtOnce => 0,

            CrossScreenTransitionType.Independent => 0,

            _ => 0
        };
    }
}

/// <summary>
/// Maps a display output to a position in a screen set grid
/// </summary>
public class ScreenPosition
{
    /// <summary>
    /// Column position (0-based, left to right)
    /// </summary>
    public int Column { get; set; }

    /// <summary>
    /// Row position (0-based, top to bottom)
    /// </summary>
    public int Row { get; set; }

    /// <summary>
    /// The output ID assigned to this position
    /// </summary>
    public string OutputId { get; set; } = string.Empty;

    /// <summary>
    /// Optional rotation in degrees (0, 90, 180, 270)
    /// </summary>
    public int Rotation { get; set; } = 0;

    /// <summary>
    /// Is this position currently connected and working?
    /// </summary>
    public bool IsConnected { get; set; }

    public ScreenPosition() { }

    public ScreenPosition(int column, int row, string outputId)
    {
        Column = column;
        Row = row;
        OutputId = outputId;
    }
}
