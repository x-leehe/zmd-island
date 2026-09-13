using System;
using Avalonia;
using Avalonia.Media;

namespace EndfieldCharge.Host.Island.Animation;

/// <summary>
/// 环弧几何：从 12 点方向顺时针画 fraction 圈（取电池徽章 / 音乐进度圆环共用）。
/// 移动自 BatteryIslandView.BuildRingGeometry，行为逐字保持。
/// </summary>
public static class RingGeometry
{
    public static Geometry Build(double fraction, double diameter, double thickness)
    {
        double radius = (diameter - thickness) / 2d;
        var center = new Point(diameter / 2d, diameter / 2d);

        double sweep = 360d * Math.Clamp(fraction, 0d, 1d);
        if (sweep < 0.5d) sweep = 0.5d;
        if (sweep > 359.5d) sweep = 359.5d;

        const double startAngle = -90d;
        var start = PointOnCircle(center, radius, startAngle);
        var end = PointOnCircle(center, radius, startAngle + sweep);

        var figure = new PathFigure { StartPoint = start, IsClosed = false };
        figure.Segments = new PathSegments
        {
            new ArcSegment
            {
                Point = end,
                Size = new Size(radius, radius),
                RotationAngle = 0d,
                IsLargeArc = sweep > 180d,
                SweepDirection = SweepDirection.Clockwise,
            },
        };

        return new PathGeometry { Figures = new PathFigures { figure } };
    }

    private static Point PointOnCircle(Point center, double radius, double degrees)
    {
        double rad = degrees * Math.PI / 180d;
        return new Point(center.X + radius * Math.Cos(rad), center.Y + radius * Math.Sin(rad));
    }
}
