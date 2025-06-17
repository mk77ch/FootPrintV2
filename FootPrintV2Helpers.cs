using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using NinjaTrader.Gui.Chart;
using NinjaTrader.NinjaScript;

namespace NinjaTrader.NinjaScript.Indicators.Infinity
{
    public class DetectedPattern
    {
        public int BarIndex { get; set; }
        public double Price { get; set; }
        public string PatternType { get; set; } = "Unknown";
        public string Details { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public int Direction { get; set; } = 0; // 1 = bullish, -1 = bearish, 0 = neutral
    }

    class RollingData
    {
        public double avgLvlAsk = 0.0;
        public double avgLvlBid = 0.0;
        public double avgLvlVol = 0.0;
        public double avgLvlDta = 0.0;
        public double avgBarAsk = 0.0;
        public double avgBarBid = 0.0;
        public double avgBarVol = 0.0;
        public double avgBarDta = 0.0;
    }

    public class PatternDetector
    {
        private readonly BarData barData;
        private readonly double tickSize;
        private readonly int lookback = 10;
        private readonly Action<string> print;
        private readonly bool log;
        private int lastProcessedBar = -1;
        private RollingData rollingData;

        private readonly object patternsLock = new object();
        private readonly List<DetectedPattern> detectedPatterns = new List<DetectedPattern>();
        public List<DetectedPattern> DetectedPatterns
        {
            get
            {
                lock (patternsLock)
                {
                    return detectedPatterns;
                }
            }
        }

        public PatternDetector(BarData barData, double tickSize, Action<string> printMethod, bool enableLogging = false)
        {
            this.barData = barData;
            this.tickSize = tickSize;
            this.print = printMethod;
            this.log = enableLogging;
        }

        public void OnBarUpdate(int currentBar)
        {
            try
            {
                if (barData?.BarItems == null || !barData.BarItems.IsValidDataPoint(0))
                    return;

                var barItem = barData.BarItems[0];
                if (barItem?.rowItems == null || barItem.rowItems.Count == 0)
                    return;

                if (currentBar != lastProcessedBar || rollingData == null)
                {
                    rollingData = GetRollingData(currentBar, lookback);
                }

                double avgLvlAsk = rollingData.avgLvlAsk;
                double avgLvlBid = rollingData.avgLvlBid;
                double avgLvlVol = rollingData.avgLvlVol;
                double avgLvlDta = rollingData.avgLvlDta;

                double avgBarAsk = rollingData.avgBarAsk;
                double avgBarBid = rollingData.avgBarBid;
                double avgBarVol = rollingData.avgBarVol;
                double avgBarDta = rollingData.avgBarDta;

                lock (patternsLock)
                {
                    DetectedPatterns.RemoveAll(p => p.BarIndex == currentBar);
                }

                if (avgLvlAsk > 0 && avgLvlBid > 0)
                {
                    DetectAbsorption(currentBar, barItem, avgLvlAsk, avgLvlBid);
                    //DetectImbalanceCluster(currentBar, barItem, avgLevelVolume);
                    //DetectExhaustion(currentBar, barItem, avgLevelVolume);
                }
            }
            catch (Exception ex)
            {
                if (log && print != null)
                    print($"PatternDetector.OnBarUpdate error: {ex.Message}");
            }
        }

        private void DetectAbsorption(int bar, dynamic barItem, double avgAskLvlVolume, double avgBidLvlVolume)
        {
            try
            {
                double askAbsorptionThreshold = avgAskLvlVolume * 2.0;
                double bidAbsorptionThreshold = avgBidLvlVolume * 2.0;

                foreach (var row in barItem.rowItems)
                {
                    if (row.Key == barItem.max && row.Key == barItem.poc) // row.Value.ask > askAbsorptionThreshold && 
                    {
                        var pattern = new DetectedPattern
                        {
                            BarIndex = bar,
                            Price = row.Key,
                            PatternType = "Absorption-Ask",
                            Direction = 1,
                            Details = $"Absorption at high: {row.Value.ask:F0} vol",
                            Timestamp = DateTime.Now
                        };

                        lock (patternsLock)
                        {
                            DetectedPatterns.Add(pattern);
                        }
                    }
                    if (row.Key == barItem.min && row.Key == barItem.poc) // row.Value.bid > bidAbsorptionThreshold && 
                    {
                        var pattern = new DetectedPattern
                        {
                            BarIndex = bar,
                            Price = row.Key,
                            PatternType = "Absorption-Bid",
                            Direction = -1,
                            Details = $"Absorption at low: {row.Value.bid:F0} vol",
                            Timestamp = DateTime.Now
                        };

                        lock (patternsLock)
                        {
                            DetectedPatterns.Add(pattern);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (log && print != null)
                    print($"DetectAbsorption error: {ex.Message}");
            }
        }

        private void DetectImbalanceCluster(int bar, dynamic barItem, double avgLevelVolume)
        {
            try
            {
                var prices = new List<double>();
                foreach (var kvp in barItem.rowItems)
                {
                    prices.Add(kvp.Key);
                }

                var sortedPrices = prices.OrderByDescending(x => x).ToList();
                int clusterCount = 0;
                for (int i = 0; i < Math.Min(5, sortedPrices.Count); i++)
                {
                    var price = sortedPrices[i];
                    NinjaTrader.NinjaScript.Indicators.Infinity.BarData.RowItem rowItem;
                    if (barItem.rowItems.TryGetValue(price, out rowItem))
                    {
                        double imbalanceRatio = rowItem.ask / Math.Max(rowItem.bid, 1);
                        if (imbalanceRatio > 3.0) clusterCount++;
                    }
                }

                if (clusterCount >= 3)
                {
                    DetectedPatterns.Add(new DetectedPattern
                    {
                        BarIndex = bar,
                        Price = sortedPrices[0],
                        PatternType = "Imbalance-Cluster",
                        Direction = 1,
                        Details = $"Cluster of {clusterCount} imbalances",
                        Timestamp = DateTime.Now
                    });

                    if (log && print != null)
                        print($"Imbalance cluster detected at bar {bar}, {clusterCount} levels");
                }
            }
            catch (Exception ex)
            {
                if (log && print != null)
                    print($"DetectImbalanceCluster error: {ex.Message}");
            }
        }

        private void DetectExhaustion(int bar, dynamic barItem, double avgLevelVolume)
        {
            try
            {
                double exhaustionThreshold = avgLevelVolume * 4.0;

                foreach (var row in barItem.rowItems)
                {
                    double totalVol = row.Value.ask + row.Value.bid;
                    if (totalVol > exhaustionThreshold)
                    {
                        string direction = row.Value.ask > row.Value.bid ? "Bullish" : "Bearish";

                        DetectedPatterns.Add(new DetectedPattern
                        {
                            BarIndex = bar,
                            Price = row.Key,
                            PatternType = $"Exhaustion-{direction}",
                            Direction = row.Value.ask > row.Value.bid ? 1 : -1,
                            Details = $"High volume: {totalVol:F0}",
                            Timestamp = DateTime.Now
                        });

                        if (log && print != null)
                            print($"Exhaustion pattern detected at bar {bar}, price {row.Key:F2}");
                    }
                }
            }
            catch (Exception ex)
            {
                if (log && print != null)
                    print($"DetectExhaustion error: {ex.Message}");
            }
        }

        private RollingData GetRollingData(int currentBar, int N)
        {
            var rollingData = new RollingData();

            double askLvlSum = 0;
            double bidLvlSum = 0;
            double volLvlSum = 0;
            double dtaLvlSum = 0;

            int askLvlCount = 0;
            int bidLvlCount = 0;
            int volLvlCount = 0;
            int dtaLvlCount = 0;

            double askBarSum = 0;
            double bidBarSum = 0;
            double volBarSum = 0;
            double dtaBarSum = 0;

            int askBarCount = 0;
            int bidBarCount = 0;
            int volBarCount = 0;
            int dtaBarCount = 0;

            for (int i = 1; i <= N; i++)
            {
                int idx = currentBar - i;
                if (idx < 0 || !barData.BarItems.IsValidDataPointAt(idx)) continue;
                var barItem = barData.BarItems.GetValueAt(idx);
                if (barItem == null || barItem.rowItems == null || barItem.rowItems.Count == 0) continue;

                foreach (var rowItem in barItem.rowItems.Values)
                {
                    if (rowItem.ask > 0)
                    {
                        askLvlSum += rowItem.ask;
                        askLvlCount++;
                    }
                    if (rowItem.bid > 0)
                    {
                        bidLvlSum += rowItem.bid;
                        bidLvlCount++;
                    }
                    if (rowItem.vol > 0)
                    {
                        volLvlSum += rowItem.vol;
                        volLvlCount++;
                    }
                    if (rowItem.dta != 0)
                    {
                        dtaLvlSum += rowItem.dta;
                        dtaLvlCount++;
                    }
                }

                if (barItem.ask > 0)
                {
                    askBarSum += barItem.ask;
                    askBarCount++;
                }
                if (barItem.bid > 0)
                {
                    bidBarSum += barItem.bid;
                    bidBarCount++;
                }
                if (barItem.vol > 0)
                {
                    volBarSum += barItem.vol;
                    volBarCount++;
                }
                if (barItem.dtc != 0)
                {
                    dtaBarSum += barItem.dtc;
                    dtaBarCount++;
                }
            }

            rollingData.avgLvlAsk = askLvlCount > 0 ? askLvlSum / askLvlCount : 1.0;
            rollingData.avgLvlBid = bidLvlCount > 0 ? bidLvlSum / bidLvlCount : 1.0;
            rollingData.avgLvlVol = volLvlCount > 0 ? volLvlSum / volLvlCount : 1.0;
            rollingData.avgLvlDta = dtaLvlCount > 0 ? dtaLvlSum / dtaLvlCount : 0.0;

            rollingData.avgBarAsk = askBarCount > 0 ? askBarSum / askBarCount : 1.0;
            rollingData.avgBarBid = bidBarCount > 0 ? bidBarSum / bidBarCount : 1.0;
            rollingData.avgBarVol = volBarCount > 0 ? volBarSum / volBarCount : 1.0;
            rollingData.avgBarDta = dtaBarCount > 0 ? dtaBarSum / dtaBarCount : 0.0;

            return rollingData;
        }
    }
    public class PatternTooltipHelper
    {
        private FootPrintV2 indicator;
        private Chart chartWindow;
        private Canvas tooltipCanvas;
        private Border tooltipBorder;
        private TextBlock tooltipText;
        private bool isDebugMode = false;

        public PatternTooltipHelper(FootPrintV2 indicator)
        {
            this.indicator = indicator;
        }

        public void Attach()
        {
            if (indicator.ChartControl == null) return;
            chartWindow = Window.GetWindow(indicator.ChartControl.Parent) as Chart;
            if (chartWindow == null) return;

            CreateTooltipCanvas();
            indicator.ChartControl.MouseMove += ChartControl_MouseMove;
        }
        private void CreateTooltipCanvas()
        {
            try
            {
                if (indicator.ChartControl.Dispatcher.CheckAccess())
                {
                    CreateUIElementsOnUIThread();
                }
                else
                {
                    indicator.ChartControl.Dispatcher.Invoke(new Action(CreateUIElementsOnUIThread));
                }
            }
            catch (Exception ex)
            {
                if (isDebugMode)
                    indicator.Print($"Error creating canvas tooltip: {ex.Message}");
            }
        }

        private void CreateUIElementsOnUIThread()
        {
            try
            {
                // Create canvas overlay
                tooltipCanvas = new Canvas
                {
                    Background = Brushes.Transparent,
                    IsHitTestVisible = false, // Canvas doesn't capture mouse events
                    Visibility = Visibility.Hidden,
                };

                // Create text block
                tooltipText = new TextBlock
                {
                    Padding = new Thickness(8, 4, 8, 4),
                    TextWrapping = TextWrapping.Wrap,
                    IsHitTestVisible = false,
                    Focusable = false,
                };

                // Create border
                tooltipBorder = new Border
                {
                    Child = tooltipText,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(2),
                    IsHitTestVisible = false,
                    Focusable = false,
                };

                // Add border to canvas
                tooltipCanvas.Children.Add(tooltipBorder);

                // Add canvas to chart's parent grid
                var chartParent = indicator.ChartControl.Parent as Panel;
                if (chartParent != null)
                {
                    chartParent.Children.Add(tooltipCanvas);
                    Panel.SetZIndex(tooltipCanvas, 1000); // Ensure it's on top
                }

                // Apply styling now that everything is on UI thread
                ApplyFallbackStylingOnUIThread();

                if (isDebugMode)
                    indicator.Print("Canvas tooltip created successfully on UI thread");
            }
            catch (Exception ex)
            {
                if (isDebugMode)
                    indicator.Print($"Error in CreateUIElementsOnUIThread: {ex.Message}");
            }
        }
        private void ApplyNinjaTraderStyling()
        {
            try
            {
                if (indicator.ChartControl.Dispatcher.CheckAccess())
                {
                    ApplyStylingOnUIThread();
                }
                else
                {
                    indicator.ChartControl.Dispatcher.BeginInvoke(new Action(ApplyStylingOnUIThread));
                }
            }
            catch (Exception ex)
            {
                if (isDebugMode)
                    indicator.Print($"Error applying NT styling: {ex.Message}");
                ApplyFallbackStyling();
            }
        }
        private void ApplyStylingOnUIThread()
        {
            try
            {
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow != null)
                {
                    var backgroundBrush = TryGetThemeResource(mainWindow, "BackgroundBrush") as Brush ??
                                         TryGetThemeResource(mainWindow, "ControlBackgroundBrush") as Brush;

                    var foregroundBrush = TryGetThemeResource(mainWindow, "FontBrush") as Brush ??
                                         TryGetThemeResource(mainWindow, "ControlForegroundBrush") as Brush;

                    var borderBrush = TryGetThemeResource(mainWindow, "BorderThinBrush") as Brush ??
                                     TryGetThemeResource(mainWindow, "ControlBorderBrush") as Brush;

                    // Apply to border with individual try-catch
                    try
                    {
                        if (backgroundBrush != null) tooltipBorder.Background = backgroundBrush;
                    }
                    catch { /* Ignore individual property errors */ }

                    try
                    {
                        if (borderBrush != null) tooltipBorder.BorderBrush = borderBrush;
                    }
                    catch { /* Ignore individual property errors */ }

                    // Apply to text with individual try-catch
                    try
                    {
                        if (foregroundBrush != null) tooltipText.Foreground = foregroundBrush;
                    }
                    catch { /* Ignore individual property errors */ }

                    var fontFamily = TryGetThemeResource(mainWindow, "MainFontFamily") as System.Windows.Media.FontFamily;
                    var fontSize = TryGetThemeResource(mainWindow, "MainFontSize");

                    try
                    {
                        if (fontFamily != null) tooltipText.FontFamily = fontFamily;
                    }
                    catch { /* Ignore individual property errors */ }

                    try
                    {
                        if (fontSize != null && fontSize is double fontSizeValue) tooltipText.FontSize = fontSizeValue;
                    }
                    catch { /* Ignore individual property errors */ }
                }

                ApplyFallbackStyling();
            }
            catch (Exception ex)
            {
                if (isDebugMode)
                    indicator.Print($"Error in ApplyStylingOnUIThread: {ex.Message}");
            }
        }

        private object TryGetThemeResource(FrameworkElement element, string resourceKey)
        {
            try
            {
                return element.TryFindResource(resourceKey);
            }
            catch
            {
                return null;
            }
        }
        private void ApplyFallbackStyling()
        {
            try
            {
                if (indicator.ChartControl.Dispatcher.CheckAccess())
                {
                    ApplyFallbackStylingOnUIThread();
                }
                else
                {
                    indicator.ChartControl.Dispatcher.BeginInvoke(new Action(ApplyFallbackStylingOnUIThread));
                }
            }
            catch (Exception ex)
            {
                if (isDebugMode)
                    indicator.Print($"Error in ApplyFallbackStyling: {ex.Message}");
            }
        }

        private void ApplyFallbackStylingOnUIThread()
        {
            try
            {
                // Apply fallback styling with individual try-catch blocks
                try
                {
                    if (tooltipBorder.Background == null)
                        tooltipBorder.Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));
                }
                catch { /* Ignore individual property errors */ }

                try
                {
                    if (tooltipBorder.BorderBrush == null)
                        tooltipBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(63, 63, 70));
                }
                catch { /* Ignore individual property errors */ }

                try
                {
                    if (tooltipText.Foreground == null)
                        tooltipText.Foreground = new SolidColorBrush(Color.FromRgb(241, 241, 241));
                }
                catch { /* Ignore individual property errors */ }

                try
                {
                    if (tooltipText.FontFamily == null)
                        tooltipText.FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
                }
                catch { /* Ignore individual property errors */ }

                try
                {
                    if (tooltipText.FontSize == 0)
                        tooltipText.FontSize = 11;
                }
                catch { /* Ignore individual property errors */ }
            }
            catch (Exception ex)
            {
                if (isDebugMode)
                    indicator.Print($"Error in ApplyFallbackStylingOnUIThread: {ex.Message}");
            }
        }

        public void Detach()
        {
            try
            {
                if (indicator.ChartControl != null)
                    indicator.ChartControl.MouseMove -= ChartControl_MouseMove;

                if (tooltipCanvas != null)
                {
                    var chartParent = indicator.ChartControl.Parent as Panel;
                    if (chartParent != null && chartParent.Children.Contains(tooltipCanvas))
                    {
                        chartParent.Children.Remove(tooltipCanvas);
                    }
                }
            }
            catch (Exception ex)
            {
                if (isDebugMode)
                    indicator.Print($"Error in detach: {ex.Message}");
            }
        }

        private void ChartControl_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                if (indicator.patternDetector == null ||
                    indicator.patternDetector.DetectedPatterns == null ||
                    indicator.patternDetector.DetectedPatterns.Count == 0)
                {
                    HideTooltip();
                    return;
                }

                Point mouse = e.GetPosition(indicator.ChartControl);
                bool patternFound = false;

                foreach (var pattern in indicator.patternDetector.DetectedPatterns)
                {
                    if (pattern.BarIndex < indicator.ChartBars.FromIndex ||
                        pattern.BarIndex > indicator.ChartBars.ToIndex)
                        continue;

                    try
                    {
                        float x = indicator.ChartControl.GetXByBarIndex(indicator.ChartBars, pattern.BarIndex);
                        float y = indicator.cScale.GetYByValue(pattern.Price);

                        double dist = Math.Sqrt(Math.Pow(mouse.X - x, 2) + Math.Pow(mouse.Y - y, 2));

                        if (dist < 15)
                        {
                            string tooltipContent = $"{pattern.PatternType ?? "Pattern"}\n{pattern.Details ?? $"Bar: {pattern.BarIndex}, Price: {pattern.Price:F2}"}";

                            ShowTooltip(tooltipContent, mouse);
                            patternFound = true;
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (isDebugMode)
                            indicator.Print($"Error processing pattern: {ex.Message}");
                        continue;
                    }
                }

                if (!patternFound)
                {
                    HideTooltip();
                }
            }
            catch (Exception ex)
            {
                indicator.Print($"PatternTooltipHelper error: {ex.Message}");
                HideTooltip();
            }
        }

        private void ShowTooltip(string content, Point mousePosition)
        {
            try
            {
                if (tooltipCanvas == null || tooltipText == null || tooltipBorder == null)
                    return;

                tooltipText.Text = content;

                // Position the tooltip
                Canvas.SetLeft(tooltipBorder, mousePosition.X + 15);
                Canvas.SetTop(tooltipBorder, mousePosition.Y - 10);

                if (tooltipCanvas.Visibility != Visibility.Visible)
                {
                    tooltipCanvas.Visibility = Visibility.Visible;
                    if (isDebugMode)
                        indicator.Print($"Canvas tooltip shown at: {mousePosition.X + 15:F0}, {mousePosition.Y - 10:F0}");
                }
            }
            catch (Exception ex)
            {
                if (isDebugMode)
                    indicator.Print($"Error showing tooltip: {ex.Message}");
            }
        }

        private void HideTooltip()
        {
            try
            {
                if (tooltipCanvas != null && tooltipCanvas.Visibility == Visibility.Visible)
                {
                    tooltipCanvas.Visibility = Visibility.Hidden;
                    if (isDebugMode)
                        indicator.Print("Canvas tooltip hidden");
                }
            }
            catch (Exception ex)
            {
                if (isDebugMode)
                    indicator.Print($"Error hiding tooltip: {ex.Message}");
            }
        }
    }
}