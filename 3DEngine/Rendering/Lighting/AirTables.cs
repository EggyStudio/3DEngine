using System.Runtime.CompilerServices;

namespace Engine;

/// <summary>
/// The Sky module's tables of an air's sky (<see cref="Sky.SkyLight.Tables"/>), packed as
/// <c>atmosphere.slang</c> reads them, a buffer of float4s made once for each air.
/// </summary>
/// <remarks>
/// A head of four float4s, the planet's radius, the air's top and the two scale heights, the
/// aerosols' asymmetry, the ozone layer's bottom and depth and the ground's lowest height, the
/// steps of a march and the two tables' rows and columns, and the lanes and where the tables'
/// cells start. Then ten float4s a lane, its sun, its four coefficients, its ground's albedo, the
/// three rows of its matrix to linear sRGB and where its ground's row starts and how long it is.
/// Then the transmittance's cells and the scattering's, row after row, a float4 a lane in each,
/// and then each lane's row of the ground's light, its logarithm with the height in degrees in w.
/// </remarks>
internal sealed class AirTables
{
    private static readonly ConditionalWeakTable<Sky.Atmosphere, AirTables> Made = new();

    private AirTables(Sky.Atmosphere air)
    {
        var tables = new Sky.SkyLight(air).Tables;
        var lanes = tables.Lanes;
        var first = lanes[0];
        var (tRows, tColumns, sRows, sColumns) = (first.TransmittanceHeights, first.TransmittanceAngles, first.ScatterHeights, first.ScatterAngles);
        var grounds = lanes.Select(lane => lane.Ground(0)).ToArray();
        var transmittances = 4 + lanes.Count * 10;
        var scatters = transmittances + tRows * tColumns * lanes.Count;
        var groundStart = scatters + sRows * sColumns * lanes.Count;
        var cells = groundStart + grounds.Sum(g => g.Heights.Length);
        var floats = new float[cells * 4];
        void Set(int at, float x, float y, float z, float w) => (floats[at * 4], floats[at * 4 + 1], floats[at * 4 + 2], floats[at * 4 + 3]) = (x, y, z, w);
        void Three(int at, System.Numerics.Vector3 v) => Set(at, v.X, v.Y, v.Z, 0);
        Set(0, tables.PlanetRadius, tables.Top, tables.RayleighScaleHeight, tables.AerosolScaleHeight);
        Set(1, tables.AerosolAnisotropy, tables.OzoneBottom, tables.OzoneWidth, tables.LowestGround);
        Set(2, tables.ViewSteps, tRows, tColumns, sRows);
        Set(3, sColumns, lanes.Count, transmittances, scatters);
        var ground = groundStart;
        for (var l = 0; l < lanes.Count; l++)
        {
            var lane = lanes[l];
            var at = 4 + l * 10;
            Three(at, lane.Sun);
            Three(at + 1, lane.RayleighScattering);
            Three(at + 2, lane.AerosolExtinction);
            Three(at + 3, lane.AerosolAlbedo);
            Three(at + 4, lane.OzoneAbsorption);
            Three(at + 5, lane.GroundAlbedo);
            var matrix = lane.ToLinearSrgb.Span;
            for (var row = 0; row < 3; row++) Set(at + 6 + row, matrix[row * 3], matrix[row * 3 + 1], matrix[row * 3 + 2], 0);
            var heights = grounds[l].Heights.Span;
            var light = grounds[l].LogLight.Span;
            Set(at + 9, ground, heights.Length, 0, 0);
            for (var i = 0; i < heights.Length; i++) Set(ground + i, light[i * 3], light[i * 3 + 1], light[i * 3 + 2], heights[i]);
            ground += heights.Length;
            var transmittance = lane.Transmittance.Span;
            var scattered = lane.Scattered.Span;
            for (var cell = 0; cell < tRows * tColumns; cell++)
                Set(transmittances + cell * lanes.Count + l, transmittance[cell * 3], transmittance[cell * 3 + 1], transmittance[cell * 3 + 2], 0);
            for (var cell = 0; cell < sRows * sColumns; cell++)
                Set(scatters + cell * lanes.Count + l, scattered[cell * 3], scattered[cell * 3 + 1], scattered[cell * 3 + 2], 0);
        }
        Floats = floats;
    }

    /// <summary>The packed tables, four floats a cell.</summary>
    public float[] Floats { get; }

    /// <summary>The tables of <paramref name="air"/>, made the first time they are
    /// asked for.</summary>
    public static AirTables Of(Sky.Atmosphere air) => Made.GetValue(air, static a => new AirTables(a));
}
