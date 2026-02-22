namespace VideoWall.Core.Enums;

/// <summary>
/// Standard single-screen transition effects
/// </summary>
public enum TransitionType
{
    // Instant
    Cut = 0,

    // Fades
    Fade = 10,
    FadeToBlack = 11,
    FadeToWhite = 12,

    // Directional Wipes
    WipeLeft = 20,
    WipeRight = 21,
    WipeUp = 22,
    WipeDown = 23,

    // Diagonal Wipes
    WipeDiagonalTL = 30,  // To top-left
    WipeDiagonalTR = 31,  // To top-right
    WipeDiagonalBL = 32,  // To bottom-left
    WipeDiagonalBR = 33,  // To bottom-right

    // Push (old content pushed off screen)
    PushLeft = 40,
    PushRight = 41,
    PushUp = 42,
    PushDown = 43,

    // Slide (new content slides over old)
    SlideLeft = 50,
    SlideRight = 51,
    SlideUp = 52,
    SlideDown = 53,

    // Iris/Circle
    IrisOpen = 60,
    IrisClose = 61,
    IrisOpenCenter = 62,
    IrisOpenCorner = 63,

    // Clock/Radial
    ClockWipe = 70,
    ClockWipeReverse = 71,
    RadialWipe = 72,

    // Dissolve/Random
    Dissolve = 80,
    DissolveRandom = 81,
    Pixelate = 82,
    PixelateRandom = 83,

    // Blur
    Blur = 90,
    BlurZoom = 91,

    // Zoom
    ZoomIn = 100,
    ZoomOut = 101,
    ZoomRotate = 102,

    // 3D Effects
    FlipHorizontal = 110,
    FlipVertical = 111,
    CubeLeft = 112,
    CubeRight = 113,
    CubeUp = 114,
    CubeDown = 115,

    // Special
    PageCurl = 120,
    PageCurlReverse = 121,
    Blinds = 122,
    BlindsVertical = 123,
    Checkerboard = 124,
    Bars = 125,
    BarsVertical = 126,

    // Morph
    Ripple = 130,
    Swirl = 131,
    Wave = 132,

    // Meta
    Random = 999
}

/// <summary>
/// Cross-screen coordinated transition effects for screen sets
/// </summary>
public enum CrossScreenTransitionType
{
    // None (each screen transitions independently)
    Independent = 0,

    // Directional wipes across entire screen set
    CrossWipeLeft = 10,
    CrossWipeRight = 11,
    CrossWipeUp = 12,
    CrossWipeDown = 13,

    // Diagonal across screen set
    CrossWipeDiagonalTL = 20,
    CrossWipeDiagonalTR = 21,
    CrossWipeDiagonalBL = 22,
    CrossWipeDiagonalBR = 23,

    // Push across all screens as one surface
    CrossPushLeft = 30,
    CrossPushRight = 31,
    CrossPushUp = 32,
    CrossPushDown = 33,

    // Sequential (one screen after another)
    SequentialLeftToRight = 40,
    SequentialRightToLeft = 41,
    SequentialTopToBottom = 42,
    SequentialBottomToTop = 43,
    SequentialSpiral = 44,
    SequentialRandom = 45,

    // Wave patterns
    WaveHorizontal = 50,
    WaveVertical = 51,
    WaveDiagonal = 52,

    // Radial from center
    Explosion = 60,    // Radiates outward from center
    Implosion = 61,    // Converges to center

    // Cascade
    CascadeTL = 70,    // Cascade from top-left
    CascadeTR = 71,
    CascadeBL = 72,
    CascadeBR = 73,

    // Synchronized
    AllAtOnce = 80,    // All screens transition simultaneously with same effect

    // Domino
    DominoLeft = 90,
    DominoRight = 91,
    DominoUp = 92,
    DominoDown = 93
}
