using System;

namespace Station
{
    /// <summary>
    /// A simple class designed to store the default values for SteamVR's chaperone_info.vrchap. These values equated to an
    /// LED ring collision bound with it's associated play area.
    /// </summary>
    public class DefaultValues
    {
        #region Circle Values
        /// <summary>
        /// The collision bounds for a circle calculated using CalculateCircleBoundary() with values: 
        /// double radius = 1.4142139;
        /// int angleIncrementDeg = 10;
        /// </summary>
        private static readonly float[][][] collisionBoundsCircle = new float[][][]
        {
            new float[][]
            {
                new float[] { 1.414214f, 0, 0.000000f },
                new float[] { 1.414214f, 2.43000007f, 0.000000f },
                new float[] { 1.392729f, 2.43000007f, 0.245576f },
                new float[] { 1.392729f, 0, 0.245576f }
            },
            new float[][]
            {
                new float[] { 1.392729f, 0, 0.245576f },
                new float[] { 1.392729f, 2.43000007f, 0.245576f },
                new float[] { 1.328926f, 2.43000007f, 0.483690f },
                new float[] { 1.328926f, 0, 0.483690f }
            },
            new float[][]
            {
                new float[] { 1.328926f, 0, 0.483690f },
                new float[] { 1.328926f, 2.43000007f, 0.483690f },
                new float[] { 1.224745f, 2.43000007f, 0.707107f },
                new float[] { 1.224745f, 0, 0.707107f }
            },
            new float[][]
            {
                new float[] { 1.224745f, 0, 0.707107f },
                new float[] { 1.224745f, 2.43000007f, 0.707107f },
                new float[] { 1.083351f, 2.43000007f, 0.909039f },
                new float[] { 1.083351f, 0, 0.909039f }
            },
            new float[][]
            {
                new float[] { 1.083351f, 0, 0.909039f },
                new float[] { 1.083351f, 2.43000007f, 0.909039f },
                new float[] { 0.909039f, 2.43000007f, 1.083351f },
                new float[] { 0.909039f, 0, 1.083351f }
            },
            new float[][]
            {
                new float[] { 0.909039f, 0, 1.083351f },
                new float[] { 0.909039f, 2.43000007f, 1.083351f },
                new float[] { 0.707107f, 2.43000007f, 1.224745f },
                new float[] { 0.707107f, 0, 1.224745f }
            },
            new float[][]
            {
                new float[] { 0.707107f, 0, 1.224745f },
                new float[] { 0.707107f, 2.43000007f, 1.224745f },
                new float[] { 0.483690f, 2.43000007f, 1.328926f },
                new float[] { 0.483690f, 0, 1.328926f }
            },
            new float[][]
            {
                new float[] { 0.483690f, 0, 1.328926f },
                new float[] { 0.483690f, 2.43000007f, 1.328926f },
                new float[] { 0.245576f, 2.43000007f, 1.392729f },
                new float[] { 0.245576f, 0, 1.392729f }
            },
            new float[][]
            {
                new float[] { 0.245576f, 0, 1.392729f },
                new float[] { 0.245576f, 2.43000007f, 1.392729f },
                new float[] { 0.000000f, 2.43000007f, 1.414214f },
                new float[] { 0.000000f, 0, 1.414214f }
            },
            new float[][]
            {
                new float[] { 0.000000f, 0, 1.414214f },
                new float[] { 0.000000f, 2.43000007f, 1.414214f },
                new float[] { -0.245576f, 2.43000007f, 1.392729f },
                new float[] { -0.245576f, 0, 1.392729f }
            },
            new float[][]
            {
                new float[] { -0.245576f, 0, 1.392729f },
                new float[] { -0.245576f, 2.43000007f, 1.392729f },
                new float[] { -0.483690f, 2.43000007f, 1.328926f },
                new float[] { -0.483690f, 0, 1.328926f }
            },
            new float[][]
            {
                new float[] { -0.483690f, 0, 1.328926f },
                new float[] { -0.483690f, 2.43000007f, 1.328926f },
                new float[] { -0.707107f, 2.43000007f, 1.224745f },
                new float[] { -0.707107f, 0, 1.224745f }
            },
            new float[][]
            {
                new float[] { -0.707107f, 0, 1.224745f },
                new float[] { -0.707107f, 2.43000007f, 1.224745f },
                new float[] { -0.909039f, 2.43000007f, 1.083351f },
                new float[] { -0.909039f, 0, 1.083351f }
            },
            new float[][]
            {
                new float[] { -0.909039f, 0, 1.083351f },
                new float[] { -0.909039f, 2.43000007f, 1.083351f },
                new float[] { -1.083351f, 2.43000007f, 0.909039f },
                new float[] { -1.083351f, 0, 0.909039f }
            },
            new float[][]
            {
                new float[] { -1.083351f, 0, 0.909039f },
                new float[] { -1.083351f, 2.43000007f, 0.909039f },
                new float[] { -1.224745f, 2.43000007f, 0.707107f },
                new float[] { -1.224745f, 0, 0.707107f }
            },
            new float[][]
            {
                new float[] { -1.224745f, 0, 0.707107f },
                new float[] { -1.224745f, 2.43000007f, 0.707107f },
                new float[] { -1.328926f, 2.43000007f, 0.483690f },
                new float[] { -1.328926f, 0, 0.483690f }
            },
            new float[][]
            {
                new float[] { -1.328926f, 0, 0.483690f },
                new float[] { -1.328926f, 2.43000007f, 0.483690f },
                new float[] { -1.392729f, 2.43000007f, 0.245576f },
                new float[] { -1.392729f, 0, 0.245576f }},
            new float[][]
            {
                new float[] { -1.392729f, 0, 0.245576f },
                new float[] { -1.392729f, 2.43000007f, 0.245576f },
                new float[] { -1.414214f, 2.43000007f, 0.000000f },
                new float[] { -1.414214f, 0, 0.000000f }
            },
            new float[][]
            {
                new float[] { -1.414214f, 0, 0.000000f },
                new float[] { -1.414214f, 2.43000007f, 0.000000f },
                new float[] { -1.392729f, 2.43000007f, -0.245576f },
                new float[] { -1.392729f, 0, -0.245576f }
            },
            new float[][]
            {
                new float[] { -1.392729f, 0, -0.245576f },
                new float[] { -1.392729f, 2.43000007f, -0.245576f },
                new float[] { -1.328926f, 2.43000007f, -0.483690f },
                new float[] { -1.328926f, 0, -0.483690f }
            },
            new float[][]
            {
                new float[] { -1.328926f, 0, -0.483690f },
                new float[] { -1.328926f, 2.43000007f, -0.483690f },
                new float[] { -1.224745f, 2.43000007f, -0.707107f },
                new float[] { -1.224745f, 0, -0.707107f }
            },
            new float[][]
            {
                new float[] { -1.224745f, 0, -0.707107f },
                new float[] { -1.224745f, 2.43000007f, -0.707107f },
                new float[] { -1.083351f, 2.43000007f, -0.909039f },
                new float[] { -1.083351f, 0, -0.909039f }
            },
            new float[][]
            {
                new float[] { -1.083351f, 0, -0.909039f },
                new float[] { -1.083351f, 2.43000007f, -0.909039f },
                new float[] { -0.909039f, 2.43000007f, -1.083351f },
                new float[] { -0.909039f, 0, -1.083351f }
            },
            new float[][]
            {
                new float[] { -0.909039f, 0, -1.083351f },
                new float[] { -0.909039f, 2.43000007f, -1.083351f },
                new float[] { -0.707107f, 2.43000007f, -1.224745f },
                new float[] { -0.707107f, 0, -1.224745f }
            },
            new float[][]
            {
                new float[] { -0.707107f, 0, -1.224745f },
                new float[] { -0.707107f, 2.43000007f, -1.224745f },
                new float[] { -0.483690f, 2.43000007f, -1.328926f },
                new float[] { -0.483690f, 0, -1.328926f }
            },
            new float[][]
            {
                new float[] { -0.483690f, 0, -1.328926f },
                new float[] { -0.483690f, 2.43000007f, -1.328926f },
                new float[] { -0.245576f, 2.43000007f, -1.392729f },
                new float[] { -0.245576f, 0, -1.392729f }
            },
            new float[][]
            {
                new float[] { -0.245576f, 0, -1.392729f },
                new float[] { -0.245576f, 2.43000007f, -1.392729f },
                new float[] { -0.000000f, 2.43000007f, -1.414214f },
                new float[] { -0.000000f, 0, -1.414214f }
            },
            new float[][]
            {
                new float[] { -0.000000f, 0, -1.414214f },
                new float[] { -0.000000f, 2.43000007f, -1.414214f },
                new float[] { 0.245576f, 2.43000007f, -1.392729f },
                new float[] { 0.245576f, 0, -1.392729f }
            },
            new float[][]
            {
                new float[] { 0.245576f, 0, -1.392729f },
                new float[] { 0.245576f, 2.43000007f, -1.392729f },
                new float[] { 0.483690f, 2.43000007f, -1.328926f },
                new float[] { 0.483690f, 0, -1.328926f }
            },
            new float[][]
            {
                new float[] { 0.483690f, 0, -1.328926f },
                new float[] { 0.483690f, 2.43000007f, -1.328926f },
                new float[] { 0.707107f, 2.43000007f, -1.224745f },
                new float[] { 0.707107f, 0, -1.224745f }
            },
            new float[][]
            {
                new float[] { 0.707107f, 0, -1.224745f },
                new float[] { 0.707107f, 2.43000007f, -1.224745f },
                new float[] { 0.909039f, 2.43000007f, -1.083351f },
                new float[] { 0.909039f, 0, -1.083351f }
            },
            new float[][]
            {
                new float[] { 0.909039f, 0, -1.083351f },
                new float[] { 0.909039f, 2.43000007f, -1.083351f },
                new float[] { 1.083351f, 2.43000007f, -0.909039f },
                new float[] { 1.083351f, 0, -0.909039f }
            },
            new float[][]
            {
                new float[] { 1.083351f, 0, -0.909039f },
                new float[] { 1.083351f, 2.43000007f, -0.909039f },
                new float[] { 1.224745f, 2.43000007f, -0.707107f },
                new float[] { 1.224745f, 0, -0.707107f }
            },
            new float[][]
            {
                new float[] { 1.224745f, 0, -0.707107f },
                new float[] { 1.224745f, 2.43000007f, -0.707107f },
                new float[] { 1.328926f, 2.43000007f, -0.483690f },
                new float[] { 1.328926f, 0, -0.483690f }
            },
            new float[][]
            {
                new float[] { 1.328926f, 0, -0.483690f },
                new float[] { 1.328926f, 2.43000007f, -0.483690f },
                new float[] { 1.392729f, 2.43000007f, -0.245576f },
                new float[] { 1.392729f, 0, -0.245576f }
            },
            new float[][]
            {
                new float[] { 1.392729f, 0, -0.245576f },
                new float[] { 1.392729f, 2.43000007f, -0.245576f },
                new float[] { 1.414214f, 2.43000007f, 0.000000f },
                new float[] { 1.414214f, 0, 0.000000f }
            }
        };

        /// <summary>
        /// The largest play area that can fit within the Circle boundary.
        /// </summary>
        private static readonly float[] playAreaCircle = new float[] 
        {
            2.100000f,
            2.100000f
        };
        #endregion

        /// <summary>
        /// Collects the collision bound default values. This can be expanded to select 
        /// from premade boundaries in the future.
        /// </summary>
        /// <returns>A float[][][] of boundary points</returns>
        public static float[][][] GetCollisionBounds()
        {
            return collisionBoundsCircle;
        }

        /// <summary>
        /// Collects the maximum play area for a set boundary. This can be expanded to select 
        /// from premade boundaries in the future.
        /// </summary>
        /// <returns>A float[] of length and width of the play area.</returns>
        public static float[] GetPlayArea()
        {
            return playAreaCircle;
        }

        #region Helpers
        /// <summary>
        /// Calculates the boundary points of a circle and a square that fits within it.
        /// The circle is defined by a specified radius, and the boundary points of the square
        /// are determined based on the circle's diameter. The circle boundary points are
        /// generated at specified angle increments in degrees.
        /// </summary>
        /// <param name="radius">The radius of the circle.</param>
        /// <param name="angleIncrementDeg">The angle increment in degrees for generating circle boundary points.</param>
        private void CalculateCircleBoundary(double radius, int angleIncrementDeg)
        {
            double diagonal = 2 * radius;
            float sideLength = (float)(diagonal / Math.Sqrt(2));

            float[] playArea = new float[2];
            playArea[0] = playArea[1] = sideLength;

            float[][][] coordinates = new float[36][][];
            double angleIncrementRad = Math.PI * angleIncrementDeg / 180;

            for (int i = 0; i < 36; i++)  // 360 degrees divided by angle increment
            {
                double angle = i * angleIncrementRad;
                double x1 = radius * Math.Cos(angle);
                double x2 = radius * Math.Cos((i + 1) * angleIncrementRad);
                double y1 = radius * Math.Sin(angle);
                double y2 = radius * Math.Sin((i + 1) * angleIncrementRad);

                coordinates[i + 1] = new float[][]
                {
                    new float[] { (float)x1, 0, (float)y1 },
                    new float[] { (float)x1, 2.43000007f, (float)y1 },
                    new float[] { (float)x2, 2.43000007f, (float)y2 },
                    new float[] { (float)x2, 0, (float)y2 }
                };
            }
        }
        #endregion
    }
}
