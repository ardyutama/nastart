namespace Nastart.Application.Tests.Features.Recipes;

// Tests the sell price formula in isolation — no DB, no handlers, pure arithmetic
[TestClass]
public sealed class DerivedSellPriceTests
{
    [TestMethod]
    [DataRow(5.00, 0.50, 0.40, 9.17)]  // (5.00+0.50)/(1-0.40) = 9.1̅ → 9.17
    [DataRow(3.20, 0.45, 0.30, 5.21)]  // (3.20+0.45)/(1-0.30) = 5.214... → 5.21
    [DataRow(2.00, 0.00, 0.50, 4.00)]  // (2.00+0.00)/(1-0.50) = 4.00
    [DataRow(5.00, 0.50, 0.00, 5.50)]  // 0% margin → sell price = cost + packaging
    public void DerivedSellPrice_Formula_MatchesExpected(
        double costPerPortion, double packagingCost, double targetMargin, double expectedSellPrice)
    {
        // Arrange
        decimal cost = (decimal)costPerPortion;
        decimal packaging = (decimal)packagingCost;
        decimal margin = (decimal)targetMargin;

        // Act — replica of the formula used in all recipe query handlers
        decimal? derivedSellPrice = margin < 1m
            ? (cost + packaging) / (1m - margin)
            : null;

        // Assert
        Assert.IsNotNull(derivedSellPrice);
        Assert.AreEqual((decimal)expectedSellPrice, Math.Round(derivedSellPrice.Value, 2));
    }

    [TestMethod]
    public void DerivedSellPrice_WhenTargetMarginIsOne_ReturnsNull()
    {
        // Arrange — 100% margin would cause division by zero
        decimal cost = 5.00m;
        decimal packaging = 0.50m;
        decimal margin = 1.0m;

        // Act
        decimal? derivedSellPrice = margin < 1m
            ? (cost + packaging) / (1m - margin)
            : null;

        // Assert — formula returns null, never throws
        Assert.IsNull(derivedSellPrice);
    }
}