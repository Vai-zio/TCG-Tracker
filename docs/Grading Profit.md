# Grading Profit

"Should I grade this card?" Logic: `Core/Valuation/GradingProfit.cs` → `GradingProfitCalculator`. Screen: `GradingProfitViewModel`.

## The idea
Compare two futures:
1. **Sell raw** now.
2. **Grade it**, pay all the costs, then sell graded.

Grading is worth it only if (2) leaves you more money than (1).

## Worked example (DKK)
| Input | Value |
|---|---|
| Raw price | 100 |
| PSA 9 price | 300 |
| Grading fee + shipping both ways + insurance | 200 |
| Marketplace fee | 13% |
| Postage when selling | 10 |

```
Raw net     = 100 − 13% − 10            =  77
PSA 9 net   = 300 − 13% − 10 − 200      =  51
Extra profit from grading = 51 − 77     = −26   ← you lose 26 by grading
```

**Break-even sale price**: the graded price where both futures are equal:
```
(77 + 200 + 10) ÷ (1 − 0.13) = 329.89
```
So a PSA 9 would need to sell for about 330 DKK to be worth it.

## What else it gives you
- **Low / base / high** scenarios, because prices are uncertain.
- **Break-even grade**: the lowest grade from which every higher grade also pays off.
- **Sensitivity**: how that changes if graded prices move ±10/20/30%.
- **Expected value**, *only* if you enter your own chances per grade adding up to 100%. The app never invents these.

## Safety checks
All amounts must be in one currency, none negative, each price low ≤ base ≤ high, fee below 100%. Otherwise it refuses to calculate.

"Fill prices from history" uses completed sales from [[Valuation]], or estimates when there are none, and says so.
