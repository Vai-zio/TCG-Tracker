using PokemonCardManager.Core.Assessment;

namespace PokemonCardManager.Core.Inspection;

public static class DefectCategories
{
    /// <summary>
    /// Which of the four areas a defect belongs to. Most types say it themselves; creases, bends and
    /// "other" can be anywhere, so the region decides (a crease through a corner is a corner problem).
    /// </summary>
    public static ConditionCategory CategoryOf(DefectType type, CardRegion region) => type switch
    {
        DefectType.CornerWhitening or DefectType.CornerRounding or DefectType.CornerChipping or DefectType.CornerBend
            => ConditionCategory.Corners,
        DefectType.EdgeWhitening or DefectType.EdgeChipping or DefectType.EdgeFraying or DefectType.EdgePeeling
            or DefectType.UnevenCut or DefectType.EdgeDent
            => ConditionCategory.Edges,
        DefectType.Crease or DefectType.Bend or DefectType.Other => region switch
        {
            CardRegion.TopLeftCorner or CardRegion.TopRightCorner or CardRegion.BottomLeftCorner or CardRegion.BottomRightCorner
                => ConditionCategory.Corners,
            CardRegion.TopEdge or CardRegion.BottomEdge or CardRegion.LeftEdge or CardRegion.RightEdge
                => ConditionCategory.Edges,
            _ => ConditionCategory.Surface,
        },
        _ => ConditionCategory.Surface,
    };

    /// <summary>Defect types offered for a category, for the annotation picker. Centering has none.</summary>
    public static IReadOnlyList<DefectType> TypesFor(ConditionCategory category)
    {
        if (category == ConditionCategory.Centering) return [];
        var typicalRegion = category switch
        {
            ConditionCategory.Corners => CardRegion.TopLeftCorner,
            ConditionCategory.Edges => CardRegion.TopEdge,
            _ => CardRegion.Surface,
        };
        return Enum.GetValues<DefectType>().Where(t => CategoryOf(t, typicalRegion) == category).ToList();
    }
}

public static class DefectRegions
{
    /// <summary>
    /// A first guess at where a marked box is, from its centre: near two edges is a corner, near one edge is
    /// that edge, otherwise the surface. "Near" is the outer 15% of the photo. The user can always correct it,
    /// and the guess assumes the card fills most of the photo.
    /// </summary>
    public static CardRegion Guess(Services.NormalizedBox box)
    {
        const double near = 0.15;
        var cx = box.X + box.Width / 2;
        var cy = box.Y + box.Height / 2;
        bool left = cx < near, right = cx > 1 - near, top = cy < near, bottom = cy > 1 - near;
        return (left, right, top, bottom) switch
        {
            (true, _, true, _) => CardRegion.TopLeftCorner,
            (_, true, true, _) => CardRegion.TopRightCorner,
            (true, _, _, true) => CardRegion.BottomLeftCorner,
            (_, true, _, true) => CardRegion.BottomRightCorner,
            (true, _, _, _) => CardRegion.LeftEdge,
            (_, true, _, _) => CardRegion.RightEdge,
            (_, _, true, _) => CardRegion.TopEdge,
            (_, _, _, true) => CardRegion.BottomEdge,
            _ => CardRegion.Surface,
        };
    }
}
