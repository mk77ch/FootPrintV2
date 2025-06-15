using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NinjaTrader.Gui.Chart;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators.Infinity;

namespace NinjaTrader.NinjaScript.Indicators.Infinity
{
    public class PatternTooltipHelper
    {
        private FootPrintV2 indicator;
        private Chart chartWindow;
        private ToolTip markerToolTip;
        private bool isDebugMode = true;

        public PatternTooltipHelper(FootPrintV2 indicator)
        {
            this.indicator = indicator;
        }

        public void Attach()
        {
            if (indicator.ChartControl == null) return;
            chartWindow = Window.GetWindow(indicator.ChartControl.Parent) as Chart;
            if (chartWindow == null) return;

            // UPDATED: Use the new styled tooltip creation
            CreateStyledTooltip();

            indicator.ChartControl.MouseMove += ChartControl_MouseMove;
        }

        // NEW: Create tooltip with NinjaTrader styling
        private void CreateStyledTooltip()
        {
            if (markerToolTip == null)
            {
                markerToolTip = new ToolTip
                {
                    Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint,
                    StaysOpen = false,
                    Visibility = Visibility.Collapsed,
                    HasDropShadow = true,
                };

                // Try to apply NinjaTrader's built-in tooltip style
                try
                {
                    var tooltipStyle = Application.Current.TryFindResource(typeof(ToolTip)) as Style;
                    if (tooltipStyle != null)
                    {
                        markerToolTip.Style = tooltipStyle;
                    }
                }
                catch (Exception ex)
                {
                    if (isDebugMode)
                        indicator.Print($"Could not apply built-in tooltip style: {ex.Message}");
                }

                // Apply NinjaTrader theme styling
                ApplyNinjaTraderStyling();
            }
        }

        // NEW: Apply NinjaTrader theme resources
        private void ApplyNinjaTraderStyling()
        {
            try
            {
                // Get NinjaTrader's main window for theme resources
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow != null)
                {
                    // Try to get NinjaTrader theme brushes
                    var backgroundBrush = TryGetThemeResource(mainWindow, "BackgroundBrush") as Brush ??
                                         TryGetThemeResource(mainWindow, "ControlBackgroundBrush") as Brush ??
                                         TryGetThemeResource(mainWindow, "WindowBackgroundBrush") as Brush;

                    var foregroundBrush = TryGetThemeResource(mainWindow, "FontBrush") as Brush ??
                                         TryGetThemeResource(mainWindow, "ControlForegroundBrush") as Brush ??
                                         TryGetThemeResource(mainWindow, "WindowForegroundBrush") as Brush;

                    var borderBrush = TryGetThemeResource(mainWindow, "BorderThinBrush") as Brush ??
                                     TryGetThemeResource(mainWindow, "ControlBorderBrush") as Brush ??
                                     TryGetThemeResource(mainWindow, "BorderBrush") as Brush;

                    // Apply found brushes
                    if (backgroundBrush != null)
                    {
                        markerToolTip.Background = backgroundBrush;
                        if (isDebugMode) indicator.Print("Applied NT background brush");
                    }

                    if (foregroundBrush != null)
                    {
                        markerToolTip.Foreground = foregroundBrush;
                        if (isDebugMode) indicator.Print("Applied NT foreground brush");
                    }

                    if (borderBrush != null)
                    {
                        markerToolTip.BorderBrush = borderBrush;
                        if (isDebugMode) indicator.Print("Applied NT border brush");
                    }

                    // Set standard properties
                    markerToolTip.BorderThickness = new Thickness(1);
                    markerToolTip.Padding = new Thickness(8, 4, 8, 4);

                    // Try to apply NinjaTrader font settings
                    var fontFamily = TryGetThemeResource(mainWindow, "MainFontFamily") as FontFamily ??
                                    TryGetThemeResource(mainWindow, "ControlFontFamily") as FontFamily;

                    var fontSize = TryGetThemeResource(mainWindow, "MainFontSize") ??
                                  TryGetThemeResource(mainWindow, "ControlFontSize");

                    if (fontFamily != null)
                    {
                        markerToolTip.FontFamily = fontFamily;
                        if (isDebugMode) indicator.Print($"Applied NT font family: {fontFamily.Source}");
                    }

                    if (fontSize != null && fontSize is double fontSizeValue)
                    {
                        markerToolTip.FontSize = fontSizeValue;
                        if (isDebugMode) indicator.Print($"Applied NT font size: {fontSizeValue}");
                    }

                    if (isDebugMode) indicator.Print("NinjaTrader styling applied successfully");
                }
                else
                {
                    if (isDebugMode) indicator.Print("MainWindow not found, applying fallback styling");
                }
            }
            catch (Exception ex)
            {
                if (isDebugMode) indicator.Print($"Error applying NT styling: {ex.Message}");
            }
            finally
            {
                // Always ensure we have some styling - apply fallback if needed
                ApplyFallbackStyling();
            }
        }

        // NEW: Helper method to safely get theme resources
        private object TryGetThemeResource(FrameworkElement element, string resourceKey)
        {
            try
            {
                var resource = element.TryFindResource(resourceKey);
                if (resource != null && isDebugMode)
                {
                    indicator.Print($"Found theme resource: {resourceKey}");
                }
                return resource;
            }
            catch (Exception ex)
            {
                if (isDebugMode)
                    indicator.Print($"Could not find theme resource '{resourceKey}': {ex.Message}");
                return null;
            }
        }

        // NEW: Fallback styling that looks like NinjaTrader
        private void ApplyFallbackStyling()
        {
            // Only apply fallback if properties aren't already set
            if (markerToolTip.Background == null)
            {
                // NinjaTrader dark theme colors
                markerToolTip.Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));
                if (isDebugMode) indicator.Print("Applied fallback background");
            }

            if (markerToolTip.Foreground == null)
            {
                markerToolTip.Foreground = new SolidColorBrush(Color.FromRgb(241, 241, 241));
                if (isDebugMode) indicator.Print("Applied fallback foreground");
            }

            if (markerToolTip.BorderBrush == null)
            {
                markerToolTip.BorderBrush = new SolidColorBrush(Color.FromRgb(63, 63, 70));
                if (isDebugMode) indicator.Print("Applied fallback border");
            }

            if (markerToolTip.BorderThickness.Left == 0)
                markerToolTip.BorderThickness = new Thickness(1);

            if (markerToolTip.Padding.Left == 0)
                markerToolTip.Padding = new Thickness(8, 4, 8, 4);

            if (markerToolTip.FontFamily == null)
            {
                markerToolTip.FontFamily = new FontFamily("Segoe UI");
                if (isDebugMode) indicator.Print("Applied fallback font family");
            }

            if (markerToolTip.FontSize == 0)
            {
                markerToolTip.FontSize = 11;
                if (isDebugMode) indicator.Print("Applied fallback font size");
            }
        }

        public void Detach()
        {
            if (indicator.ChartControl != null)
                indicator.ChartControl.MouseMove -= ChartControl_MouseMove;
            if (markerToolTip != null)
            {
                markerToolTip.IsOpen = false;
            }
        }

        // EXISTING: Your existing ChartControl_MouseMove method stays the same
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

                // Check all visible patterns for proximity to mouse
                foreach (var pattern in indicator.patternDetector.DetectedPatterns)
                {
                    // Skip patterns outside visible range
                    if (pattern.BarIndex < indicator.ChartBars.FromIndex ||
                        pattern.BarIndex > indicator.ChartBars.ToIndex)
                        continue;

                    try
                    {
                        // Get pattern screen coordinates
                        float x = indicator.ChartControl.GetXByBarIndex(indicator.ChartBars, pattern.BarIndex);
                        float y = indicator.cScale.GetYByValue(pattern.Price);

                        // Calculate distance from mouse to pattern
                        double dist = Math.Sqrt(Math.Pow(mouse.X - x, 2) + Math.Pow(mouse.Y - y, 2));

                        // Show tooltip if close enough (within 30 pixels)
                        if (dist < 30)
                        {
                            string tooltipContent = $"{pattern.PatternType ?? "Pattern"}\n{pattern.Details ?? $"Bar: {pattern.BarIndex}, Price: {pattern.Price:F2}"}";

                            markerToolTip.Content = tooltipContent;
                            markerToolTip.Visibility = Visibility.Visible;
                            markerToolTip.IsOpen = true;

                            if (isDebugMode)
                            {
                                indicator.Print($"Tooltip shown: {tooltipContent}");
                            }

                            return; // Show only the first matching pattern
                        }
                    }
                    catch (Exception ex)
                    {
                        if (isDebugMode)
                            indicator.Print($"Error processing pattern at bar {pattern.BarIndex}: {ex.Message}");
                        continue;
                    }
                }

                // No patterns found near mouse - hide tooltip
                HideTooltip();
            }
            catch (Exception ex)
            {
                indicator.Print($"PatternTooltipHelper error: {ex.Message}");
                HideTooltip();
            }
        }

        private void HideTooltip()
        {
            if (markerToolTip != null)
            {
                markerToolTip.IsOpen = false;
                markerToolTip.Visibility = Visibility.Collapsed;
            }
        }
    }
}