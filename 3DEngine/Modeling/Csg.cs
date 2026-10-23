// The binary space partition below is csg.js's, written in C# after it:
//
//   csg.js, Constructive Solid Geometry (CSG) in JavaScript
//   Copyright (c) 2011 Evan Wallace (http://madebyevan.com/), under the MIT license.
//
//   Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
//   associated documentation files (the "Software"), to deal in the Software without restriction,
//   including without limitation the rights to use, copy, modify, merge, publish, distribute,
//   sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is
//   furnished to do so, subject to the following conditions:
//
//   The above copyright notice and this permission notice shall be included in all copies or
//   substantial portions of the Software.
//
//   THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT
//   NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
//   NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
//   DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT
//   OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

namespace Engine;

/// <summary>
/// Union, difference and intersection of two closed solids given as convex polygons, by csg.js's
/// binary space partition, which an edit mesh's booleans run on its faces cut into triangles.
/// </summary>
/// <remarks>
/// <para>
/// Each solid is made a tree of the planes of its polygons, every polygon split by the planes it
/// crosses, and each solid's polygons clipped against the other's tree, so what lies inside the
/// other, or outside it, is dropped as the operation keeps one or the other. A polygon in a plane of
/// the other solid goes to the side its own normal faces, which is how two faces in one plane are
/// cut, and why an opening is cut with a solid a little deeper than the wall it goes through.
/// </para>
/// <para>
/// A point is in a plane where it is within the epsilon of it, which the mesh holds, a hundredth of
/// a millimeter unless it says another. Worked in doubles, so a level's walls far from the origin
/// split where they meet. A polygon carries what it came from, its slot, its smoothing and whether
/// it was the second solid's, through every split, so the faces made keep their slots.
/// </para>
/// </remarks>
internal static class Csg
{
    /// <summary>A point in doubles.</summary>
    internal readonly record struct Point(double X, double Y, double Z)
    {
        public static Point operator +(Point a, Point b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Point operator -(Point a, Point b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Point operator *(Point a, double s) => new(a.X * s, a.Y * s, a.Z * s);
        public static double Dot(Point a, Point b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public static Point Cross(Point a, Point b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        public double Length => Math.Sqrt(Dot(this, this));
        public Point Unit => this * (1.0 / Length);
    }

    /// <summary>What a polygon came from, carried through every split.</summary>
    internal readonly record struct Source(string Slot, float Smooth, bool Second);

    /// <summary>A plane, its normal of a length of one and how far along it from the origin.</summary>
    internal sealed class Plane(Point normal, double w)
    {
        public Point Normal = normal;
        public double W = w;

        public static Plane Through(Point a, Point b, Point c)
        {
            var normal = Point.Cross(b - a, c - a).Unit;
            return new Plane(normal, Point.Dot(normal, a));
        }

        public Plane Clone() => new(Normal, W);

        public void Flip()
        {
            Normal = Normal * -1;
            W = -W;
        }

        private const int Coplanar = 0, Front = 1, Back = 2, Spanning = 3;

        /// <summary>
        /// Puts a polygon in the list for the side of the plane it lies on, a polygon in the plane in
        /// the coplanar list for the way it faces, and one across it split into a part on each side.
        /// </summary>
        public void Split(Polygon polygon, double epsilon, List<Polygon> coplanarFront, List<Polygon> coplanarBack, List<Polygon> front, List<Polygon> back)
        {
            var kinds = new int[polygon.Points.Length];
            var all = 0;
            for (int i = 0; i < kinds.Length; i++)
            {
                var t = Point.Dot(Normal, polygon.Points[i]) - W;
                kinds[i] = t < -epsilon ? Back : t > epsilon ? Front : Coplanar;
                all |= kinds[i];
            }

            switch (all)
            {
                case Coplanar:
                    (Point.Dot(Normal, polygon.Plane.Normal) > 0 ? coplanarFront : coplanarBack).Add(polygon);
                    return;
                case Front:
                    front.Add(polygon);
                    return;
                case Back:
                    back.Add(polygon);
                    return;
            }

            var ahead = new List<Point>();
            var behind = new List<Point>();
            for (int i = 0; i < kinds.Length; i++)
            {
                var j = (i + 1) % kinds.Length;
                var (here, next) = (polygon.Points[i], polygon.Points[j]);
                if (kinds[i] != Back) ahead.Add(here);
                if (kinds[i] != Front) behind.Add(here);
                if ((kinds[i] | kinds[j]) == Spanning)
                {
                    var t = (W - Point.Dot(Normal, here)) / Point.Dot(Normal, next - here);
                    var crossing = here + (next - here) * t;
                    ahead.Add(crossing);
                    behind.Add(crossing);
                }
            }

            if (ahead.Count >= 3) front.Add(new Polygon([.. ahead], polygon.Source, polygon.Plane));
            if (behind.Count >= 3) back.Add(new Polygon([.. behind], polygon.Source, polygon.Plane));
        }
    }

    /// <summary>A convex polygon, its corners counterclockwise about its plane's normal.</summary>
    internal sealed class Polygon
    {
        public Point[] Points;
        public readonly Source Source;
        public Plane Plane;

        public Polygon(Point[] points, Source source, Plane? plane = null)
        {
            Points = points;
            Source = source;
            Plane = plane?.Clone() ?? Plane.Through(points[0], points[1], points[2]);
        }

        public void Flip()
        {
            Array.Reverse(Points);
            Plane.Flip();
        }
    }

    /// <summary>A node of the tree, a plane with the polygons in it and the trees in front of it and behind.</summary>
    private sealed class Node
    {
        private Plane? _plane;
        private Node? _front;
        private Node? _back;
        private List<Polygon> _polygons = [];
        private readonly double _epsilon;

        public Node(IEnumerable<Polygon> polygons, double epsilon)
        {
            _epsilon = epsilon;
            Build([.. polygons]);
        }

        private Node(double epsilon) => _epsilon = epsilon;

        /// <summary>Turns the solid inside out, every polygon and plane flipped and front and back traded.</summary>
        public void Invert()
        {
            foreach (var polygon in _polygons) polygon.Flip();
            _plane?.Flip();
            _front?.Invert();
            _back?.Invert();
            (_front, _back) = (_back, _front);
        }

        /// <summary>The parts of polygons outside this tree's solid.</summary>
        public List<Polygon> Clip(List<Polygon> polygons)
        {
            if (_plane is null) return [.. polygons];
            var front = new List<Polygon>();
            var back = new List<Polygon>();
            foreach (var polygon in polygons) _plane.Split(polygon, _epsilon, front, back, front, back);
            if (_front is not null) front = _front.Clip(front);
            back = _back is not null ? _back.Clip(back) : [];
            front.AddRange(back);
            return front;
        }

        /// <summary>Takes away the parts of this tree's polygons inside another tree's solid.</summary>
        public void ClipTo(Node other)
        {
            _polygons = other.Clip(_polygons);
            _front?.ClipTo(other);
            _back?.ClipTo(other);
        }

        public List<Polygon> All()
        {
            var all = new List<Polygon>(_polygons);
            if (_front is not null) all.AddRange(_front.All());
            if (_back is not null) all.AddRange(_back.All());
            return all;
        }

        /// <summary>Adds polygons to the tree, each split down to the node whose plane it lies in.</summary>
        public void Build(List<Polygon> polygons)
        {
            if (polygons.Count == 0) return;
            _plane ??= polygons[0].Plane.Clone();
            var front = new List<Polygon>();
            var back = new List<Polygon>();
            foreach (var polygon in polygons) _plane.Split(polygon, _epsilon, _polygons, _polygons, front, back);
            if (front.Count > 0) (_front ??= new Node(_epsilon)).Build(front);
            if (back.Count > 0) (_back ??= new Node(_epsilon)).Build(back);
        }
    }

    /// <summary>What lies in either solid.</summary>
    public static List<Polygon> Union(IEnumerable<Polygon> first, IEnumerable<Polygon> second, double epsilon)
    {
        var a = new Node(first, epsilon);
        var b = new Node(second, epsilon);
        a.ClipTo(b);
        b.ClipTo(a);
        b.Invert();
        b.ClipTo(a);
        b.Invert();
        a.Build(b.All());
        return a.All();
    }

    /// <summary>What lies in the first solid and not the second.</summary>
    public static List<Polygon> Subtract(IEnumerable<Polygon> first, IEnumerable<Polygon> second, double epsilon)
    {
        var a = new Node(first, epsilon);
        var b = new Node(second, epsilon);
        a.Invert();
        a.ClipTo(b);
        b.ClipTo(a);
        b.Invert();
        b.ClipTo(a);
        b.Invert();
        a.Build(b.All());
        a.Invert();
        return a.All();
    }

    /// <summary>What lies in both solids.</summary>
    public static List<Polygon> Intersect(IEnumerable<Polygon> first, IEnumerable<Polygon> second, double epsilon)
    {
        var a = new Node(first, epsilon);
        var b = new Node(second, epsilon);
        a.Invert();
        b.ClipTo(a);
        b.Invert();
        a.ClipTo(b);
        b.ClipTo(a);
        a.Build(b.All());
        a.Invert();
        return a.All();
    }
}
