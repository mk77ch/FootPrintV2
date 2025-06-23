using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace VolumeDataAnalysis
{
    class Program
    {
        static void Main(string[] args)
        {
            // Your volume data
            var volumes = new List<double>
            {
                38, 10, 19, 1, 0, 0, 6, 24, 34, 38, 71, 65, 66, 70, 33, 46, 38, 8, 9, 0,
                23, 11, 19, 30, 19, 45, 40, 71, 165, 176, 109, 71, 104, 105, 125, 120, 95, 39, 20, 7,
                41, 1, 17, 22, 0, 9, 30, 75, 55, 26, 22, 22, 34, 41, 19, 25, 45, 40, 36, 43,
                71, 41, 29, 19, 15, 5, 114, 111, 103, 37, 37, 9, 6, 0, 1, 16, 8, 7, 6, 27,
                34, 58, 28, 21, 21, 53, 66, 84, 5, 66, 55, 53, 49, 45, 13, 78, 84, 119, 91, 83,
                51, 75, 65, 55, 27, 8, 24, 23, 23, 0, 48, 70, 11, 9, 10, 3, 0, 2, 4, 27,
                5, 43, 61, 112, 114, 115, 135, 95, 48, 60, 39, 58, 36, 54, 74, 94, 52, 42, 59, 44,
                76, 47, 13, 35, 13, 6, 2, 18, 0, 17, 0, 17, 20, 37, 1, 49, 1, 53, 13, 47,
                28, 61, 41, 83, 23, 44, 23, 66, 58, 52, 29, 41, 23, 21, 5, 2, 4, 14, 5, 31,
                40, 8, 19, 0, 11, 0, 10, 10, 28, 9, 9, 17, 22, 7, 0, 30, 18, 37, 1, 15,
                4, 9, 10, 25, 12, 55, 91, 83, 96, 99, 93, 39, 33, 21, 20, 30, 19, 2, 4, 0,
                34, 90, 89, 119, 80, 181, 114, 112, 95, 54, 17, 18, 31, 16, 6, 0, 0, 0, 2, 65,
                11, 37, 47, 48, 37, 11, 61, 21, 39, 5, 7, 7, 0, 0, 0, 0, 8, 28, 25, 18,
                13, 8, 21, 20, 13, 66, 63, 121, 92, 129, 66, 78, 61, 69, 124, 74, 58, 27, 16, 0,
                0, 6, 6, 22, 14, 31, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 46, 0, 13, 91, 116, 71, 77, 96, 75, 30, 34, 18, 41, 1, 34, 1,
                18, 9, 4, 4, 27, 7, 92, 32, 99, 76, 59, 58, 12, 19, 5, 14, 0, 0, 47, 18,
                52, 8, 33, 35, 62, 51, 70, 72, 84, 173, 101, 0, 0, 0, 6, 10, 18, 18, 17, 32,
                25, 38, 34, 35, 42, 42, 5, 0, 0, 0, 0, 0, 0, 55, 39, 4, 12, 1, 35, 10,
                52, 68, 115, 57, 54, 108, 125, 159, 110, 61, 68, 32, 71, 66, 53, 11, 58, 26, 60, 39,
                44, 35, 52, 22, 26, 18, 53, 0, 13, 0, 0, 19, 111, 1, 27, 6, 33, 0, 50, 13,
                43, 47, 72, 88, 127, 129, 163, 141, 178, 131, 124, 105, 148, 74, 45, 45, 24, 3, 0, 17,
                21, 9, 22, 26, 32, 0, 2, 0, 8, 6, 22, 3, 42, 68, 56, 43, 70, 45, 23, 20,
                42, 16, 71, 7, 63, 26, 59, 48, 58, 43, 84, 53, 57, 24, 32, 29, 32, 15, 12, 0,
                17, 0, 26, 0, 3, 0, 9, 0, 22, 23, 12, 19, 11, 29, 4, 19, 5, 27, 3, 14,
                9, 14, 11, 12, 3, 140, 133, 85, 69, 73, 36, 48, 76, 107, 71, 25, 0, 14, 0, 0,
                2, 1, 4, 14, 20, 25, 69, 0, 15, 15, 40, 39, 31, 20, 17, 44, 43, 81, 93, 65,
                99, 35, 36, 52, 38, 82, 50, 36, 0, 19, 21, 41, 0, 1, 0, 0, 0, 0, 0, 0,
                0, 23, 37, 41, 34, 25, 18, 24, 18, 22, 33, 0, 23, 5, 29, 34, 26, 34, 51, 38,
                27, 32, 53, 44, 54, 14, 99, 35, 63, 49, 50, 21, 17, 40, 57, 27, 56, 23, 34, 46,
                33, 33, 35, 43, 23, 21, 11, 13, 3, 7, 0, 0, 0, 0, 7, 25, 1, 8, 0, 0,
                0, 8, 55, 89, 116, 90, 126, 144, 134, 115, 167, 194, 147, 113, 56, 66, 13, 13, 0, 0,
                5, 4, 27, 35, 44, 11, 28, 31, 60, 60, 16, 11, 9, 45, 36, 48, 11, 34, 2, 10,
                18, 44, 69, 76, 51, 38, 48, 32, 34, 46, 56, 50, 11, 63, 47, 65, 32, 54, 6, 19,
                0, 0, 13, 28, 70, 33, 46, 73, 87, 98, 84, 95, 136, 151, 208, 204, 290, 338, 229, 140,
                69, 94, 20, 9, 0, 0, 14, 5, 12, 3, 4, 0, 5, 17, 32, 17, 38, 58, 80, 103,
                83, 52, 41, 72, 24, 0, 0, 0, 31, 9, 25, 17, 12, 7, 25, 8, 23, 11, 16, 5,
                19, 18, 29, 25, 9, 2, 0, 102, 95, 79, 42, 35, 27, 21, 2, 0, 33, 55, 76, 83,
                71, 53, 60, 122, 146, 124, 189, 112, 88, 2, 7, 8, 12, 23, 51, 6, 5, 1, 0, 0,
                19, 5, 6
            };

            AnalyzeVolumeDistribution(volumes);
            CompareScalingMethods(volumes);
        }

        static void AnalyzeVolumeDistribution(List<double> volumes)
        {
            Console.WriteLine("=== VOLUME DATA ANALYSIS ===\n");

            var nonZeroVolumes = volumes.Where(v => v > 0).ToList();

            Console.WriteLine($"Total values: {volumes.Count}");
            Console.WriteLine($"Non-zero values: {nonZeroVolumes.Count}");
            Console.WriteLine($"Zero values: {volumes.Count - nonZeroVolumes.Count} ({((volumes.Count - nonZeroVolumes.Count) * 100.0 / volumes.Count):F1}%)\n");

            Console.WriteLine("=== BASIC STATISTICS ===");
            Console.WriteLine($"Minimum: {volumes.Min()}");
            Console.WriteLine($"Maximum: {volumes.Max()}");
            Console.WriteLine($"Average: {volumes.Average():F1}");
            Console.WriteLine($"Median: {GetMedian(volumes):F1}");
            Console.WriteLine($"Standard Deviation: {GetStandardDeviation(volumes):F1}\n");

            Console.WriteLine("=== NON-ZERO STATISTICS ===");
            Console.WriteLine($"Min (non-zero): {nonZeroVolumes.Min()}");
            Console.WriteLine($"Max (non-zero): {nonZeroVolumes.Max()}");
            Console.WriteLine($"Average (non-zero): {nonZeroVolumes.Average():F1}");
            Console.WriteLine($"Median (non-zero): {GetMedian(nonZeroVolumes):F1}\n");

            Console.WriteLine("=== PERCENTILES ===");
            var sortedNonZero = nonZeroVolumes.OrderBy(v => v).ToList();
            Console.WriteLine($"10th percentile: {GetPercentile(sortedNonZero, 10):F1}");
            Console.WriteLine($"25th percentile: {GetPercentile(sortedNonZero, 25):F1}");
            Console.WriteLine($"75th percentile: {GetPercentile(sortedNonZero, 75):F1}");
            Console.WriteLine($"90th percentile: {GetPercentile(sortedNonZero, 90):F1}");
            Console.WriteLine($"95th percentile: {GetPercentile(sortedNonZero, 95):F1}");
            Console.WriteLine($"99th percentile: {GetPercentile(sortedNonZero, 99):F1}\n");

            Console.WriteLine("=== DISTRIBUTION ANALYSIS ===");
            AnalyzeDistributionRanges(nonZeroVolumes);
        }

        static void AnalyzeDistributionRanges(List<double> volumes)
        {
            var max = volumes.Max();
            var ranges = new[]
            {
                new { Name = "0-10% of max", Min = 0.0, Max = max * 0.1 },
                new { Name = "10-25% of max", Min = max * 0.1, Max = max * 0.25 },
                new { Name = "25-50% of max", Min = max * 0.25, Max = max * 0.5 },
                new { Name = "50-75% of max", Min = max * 0.5, Max = max * 0.75 },
                new { Name = "75-90% of max", Min = max * 0.75, Max = max * 0.9 },
                new { Name = "90-100% of max", Min = max * 0.9, Max = max }
            };

            foreach (var range in ranges)
            {
                var count = volumes.Count(v => v > range.Min && v <= range.Max);
                var percentage = count * 100.0 / volumes.Count;
                Console.WriteLine($"{range.Name}: {count} values ({percentage:F1}%) - Range: {range.Min:F1} to {range.Max:F1}");
            }
        }

        static void CompareScalingMethods(List<double> volumes)
        {
            Console.WriteLine("\n=== SCALING COMPARISON ===");

            var maxVol = volumes.Max();
            var testVolumes = new[] { 1.0, 10.0, 50.0, 100.0, 200.0, maxVol };
            var maxWidth = 100.0; // Assume 100px max width

            Console.WriteLine("Volume -> Linear% -> Log2% -> LogE% -> Log10%");
            Console.WriteLine("----------------------------------------------");

            foreach (var vol in testVolumes)
            {
                var linear = (vol / maxVol) * 100;
                var log2 = (Math.Log(vol + 1, 2) / Math.Log(maxVol + 1, 2)) * 100;
                var logE = (Math.Log(vol + 1, Math.E) / Math.Log(maxVol + 1, Math.E)) * 100;
                var log10 = (Math.Log(vol + 1, 10) / Math.Log(maxVol + 1, 10)) * 100;

                Console.WriteLine($"{vol,6:F0} -> {linear,6:F1}% -> {log2,6:F1}% -> {logE,6:F1}% -> {log10,6:F1}%");
            }

            Console.WriteLine("\n=== IMPROVEMENT ANALYSIS ===");
            AnalyzeImprovementRatio(volumes, maxVol);
        }

        static void AnalyzeImprovementRatio(List<double> volumes, double maxVol)
        {
            var nonZeroVolumes = volumes.Where(v => v > 0).ToList();
            var sortedVolumes = nonZeroVolumes.OrderBy(v => v).ToList();

            // Compare smallest 25% vs largest value
            var bottomQuartileCount = sortedVolumes.Count / 4;
            var smallVolumes = sortedVolumes.Take(bottomQuartileCount).ToList();
            var avgSmallVolume = smallVolumes.Average();

            Console.WriteLine($"Bottom 25% average volume: {avgSmallVolume:F1}");
            Console.WriteLine($"Maximum volume: {maxVol:F0}");
            Console.WriteLine($"Ratio (max/small): {maxVol / avgSmallVolume:F1}:1\n");

            Console.WriteLine("Linear scaling visibility for small volumes:");
            Console.WriteLine($"Small volume width: {(avgSmallVolume / maxVol) * 100:F2}% of max width");

            Console.WriteLine("\nLogarithmic scaling visibility (base e):");
            var logSmall = Math.Log(avgSmallVolume + 1, Math.E);
            var logMax = Math.Log(maxVol + 1, Math.E);
            Console.WriteLine($"Small volume width: {(logSmall / logMax) * 100:F1}% of max width");

            var improvement = ((logSmall / logMax) / (avgSmallVolume / maxVol));
            Console.WriteLine($"Improvement factor: {improvement:F1}x better visibility");
        }

        static double GetMedian(List<double> values)
        {
            var sorted = values.OrderBy(v => v).ToList();
            var mid = sorted.Count / 2;
            return sorted.Count % 2 == 0 ? (sorted[mid - 1] + sorted[mid]) / 2 : sorted[mid];
        }

        static double GetStandardDeviation(List<double> values)
        {
            var avg = values.Average();
            var sumSquaredDiffs = values.Sum(v => Math.Pow(v - avg, 2));
            return Math.Sqrt(sumSquaredDiffs / values.Count);
        }

        static double GetPercentile(List<double> sortedValues, int percentile)
        {
            var index = (percentile / 100.0) * (sortedValues.Count - 1);
            var lower = (int)Math.Floor(index);
            var upper = (int)Math.Ceiling(index);

            if (lower == upper) return sortedValues[lower];

            var weight = index - lower;
            return sortedValues[lower] * (1 - weight) + sortedValues[upper] * weight;
        }
    }
}