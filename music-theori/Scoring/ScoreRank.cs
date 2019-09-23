namespace theori.Scoring
{
    /// <summary>
    /// Provides a large assortment of rankings for total chart judgements.
    /// There are 8 categories, the middle 6 of which have 6 values.
    /// 
    /// By convention, the important provided ranks mean this:
    /// > Perfect is perfect.
    /// > X suffix is a higher variant of the rank.
    /// > S is near perfect.
    /// > A is great play.
    /// > B is average play.
    /// > C and below are poor play.
    /// > F is failure, extremely poor play.
    /// </summary>
    public enum ScoreRank
    {
        Perfect,

        SSSX,
        SSS,
        SSX,
        SS,
        SX,
        S,

        AAAX,
        AAA,
        AAX,
        AA,
        AX,
        A,

        BBBX,
        BBB,
        BBX,
        BB,
        BX,
        B,

        CCCX,
        CCC,
        CCX,
        CC,
        CX,
        C,

        DDDX,
        DDD,
        DDX,
        DD,
        DX,
        D,

        EEEX,
        EEE,
        EEX,
        EE,
        EX,
        E,

        F
    }
}
