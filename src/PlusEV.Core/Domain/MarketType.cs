namespace PlusEV.Core.Domain;

/// <summary>The betting market a price belongs to.</summary>
public enum MarketType
{
    /// <summary>Moneyline / match result.</summary>
    H2H = 0,

    /// <summary>Point spread / handicap.</summary>
    Spread = 1,

    /// <summary>Over/under total.</summary>
    Total = 2,

    /// <summary>Any other prop or derivative.</summary>
    Prop = 3,
}
