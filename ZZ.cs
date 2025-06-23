#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

namespace NinjaTrader.NinjaScript.Indicators.Infinity
{
    /// <summary>
    /// The ZigZag indicator shows trend lines filtering out changes below a defined level.
    /// </summary>
    public class ZZ : Indicator
    {
        private Series<double> ZigZagLo;
        private Series<double> ZigZagHi;

        private int zzDir = 0;
        private int lastLoBar = 0;
        private int lastHiBar = 0;
        private double lastLoVal = 0.0;
        private double lastHiVal = 0.0;

        // OnStateChange
        //
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = NinjaTrader.Custom.Resource.NinjaScriptIndicatorDescriptionZigZag;
                Name = "ZZ";
                Calculate = Calculate.OnPriceChange;
                DisplayInDataBox = false;
                DrawOnPricePanel = false;
                IsSuspendedWhileInactive = true;
                IsOverlay = true;
                PaintPriceMarkers = false;
                DisplayInDataBox = false;
                PaintPriceMarkers = false;
                MaximumBarsLookBack = MaximumBarsLookBack.Infinite;

                zzSpan = 4;

                AddPlot(Brushes.Yellow, NinjaTrader.Custom.Resource.NinjaScriptIndicatorNameZigZag);
            }
            else if (State == State.Configure)
            {
            }
            else if (State == State.DataLoaded)
            {
                ZigZagLo = new Series<double>(this);
                ZigZagHi = new Series<double>(this);
            }
        }

        // ---

        private bool isSwingPoint(int barIndex)
        {
            if (barIndex < 0) return false;
            if (barIndex > Count - 1) return false;

            return ZigZagDots.IsValidDataPointAt(barIndex);
        }

        // ---

        private bool isTempSwingPoint(int barIndex)
        {
            bool temp = true;

            for (int i = barIndex + 1; i <= Count - 1; i++)
            {
                if (isSwingPoint(i))
                {
                    temp = false;
                    break;
                }
            }

            return temp;
        }

        public bool isSwingLow(int barIndex, bool allowTempSwingPoints = true)
        {
            if (barIndex < 0) return false;
            if (barIndex > Count - 1) return false;

            if (ZigZagDots.IsValidDataPointAt(barIndex))
            {
                if (ZigZagDots.GetValueAt(barIndex) == Bars.GetLow(barIndex))
                {
                    return true;
                }
            }

            return false;
        }

        public bool isSwingHigh(int barIndex, bool allowTempSwingPoints = true)
        {
            if (barIndex < 0) return false;
            if (barIndex > Count - 1) return false;

            if (ZigZagDots.IsValidDataPointAt(barIndex))
            {
                if (ZigZagDots.GetValueAt(barIndex) == Bars.GetHigh(barIndex))
                {
                    return true;
                }
            }

            return false;
        }

        public int getNextSwingLowBar(int barIndex, bool allowTempSwingPoints = true)
        {
            int bar = -1;

            for (int i = barIndex + 1; i <= Count - 1; i++)
            {
                if (isSwingLow(i))
                {
                    if (!allowTempSwingPoints && isTempSwingPoint(i)) continue;
                    bar = i;
                    break;
                }
            }

            return bar;
        }

        public int getLastSwingLowBar(int barIndex, bool allowTempSwingPoints = true)
        {
            int bar = -1;

            for (int i = barIndex - 1; i >= 0; i--)
            {
                if (isSwingLow(i))
                {
                    if (!allowTempSwingPoints && isTempSwingPoint(i)) continue;
                    bar = i;
                    break;
                }
            }

            return bar;
        }

        public int getLastSwingHighBar(int barIndex, bool allowTempSwingPoints = true)
        {
            int bar = -1;

            for (int i = barIndex - 1; i >= 0; i--)
            {
                if (isSwingHigh(i))
                {
                    if (!allowTempSwingPoints && isTempSwingPoint(i)) continue;
                    bar = i;
                    break;
                }
            }

            return bar;
        }

        public int getNextSwingHighBar(int barIndex, bool allowTempSwingPoints = true)
        {
            int bar = -1;

            for (int i = barIndex + 1; i <= Count - 1; i++)
            {
                if (isSwingHigh(i))
                {
                    if (!allowTempSwingPoints && isTempSwingPoint(i)) continue;
                    bar = i;
                    break;
                }
            }

            return bar;
        }

        // OnBarUpdate
        //
        protected override void OnBarUpdate()
        {
            if (CurrentBar < 0) { return; }

            if (CurrentBar == 0)
            {
                lastLoVal = Low[0];
                lastHiVal = High[0];
            }
            else
            {
                ZigZagLo[0] = MIN(Low, zzSpan)[0];
                ZigZagHi[0] = MAX(High, zzSpan)[0];

                if (zzDir == 0)
                {
                    if (ZigZagLo[0] < lastLoVal)
                    {
                        lastLoVal = ZigZagLo[0];
                        lastLoBar = CurrentBar;

                        if (ZigZagHi[0] < ZigZagHi[1])
                        {
                            zzDir = -1;
                        }
                    }
                    if (ZigZagHi[0] > lastHiVal)
                    {
                        lastHiVal = ZigZagHi[0];
                        lastHiBar = CurrentBar;

                        if (ZigZagLo[0] > ZigZagLo[1])
                        {
                            zzDir = 1;
                        }
                    }
                }

                if (zzDir > 0)
                {
                    if (ZigZagHi[0] > lastHiVal)
                    {
                        ZigZagDots.Reset(CurrentBar - lastHiBar);

                        lastHiVal = ZigZagHi[0];
                        lastHiBar = CurrentBar;

                        if (Plots[0].Brush != Brushes.Transparent)
                        {
                            Draw.Line(this, lastLoBar.ToString(), CurrentBar - lastLoBar, lastLoVal, CurrentBar - lastHiBar, lastHiVal, Plots[0].Brush);
                        }

                        ZigZagDots[CurrentBar - lastHiBar] = lastHiVal;
                    }
                    else if (ZigZagHi[0] < lastHiVal && ZigZagLo[0] < ZigZagLo[1])
                    {
                        if (Plots[0].Brush != Brushes.Transparent)
                        {
                            Draw.Line(this, lastLoBar.ToString(), CurrentBar - lastLoBar, lastLoVal, CurrentBar - lastHiBar, lastHiVal, Plots[0].Brush);
                        }

                        ZigZagDots[CurrentBar - lastHiBar] = lastHiVal;

                        zzDir = -1;
                        lastLoVal = ZigZagLo[0];
                        lastLoBar = CurrentBar;

                        if (Plots[0].Brush != Brushes.Transparent)
                        {
                            Draw.Line(this, lastHiBar.ToString(), CurrentBar - lastHiBar, lastHiVal, CurrentBar - lastLoBar, lastLoVal, Plots[0].Brush);
                        }

                        ZigZagDots[CurrentBar - lastLoBar] = lastLoVal;
                    }
                }
                else
                {
                    if (ZigZagLo[0] < lastLoVal)
                    {
                        ZigZagDots.Reset(CurrentBar - lastLoBar);

                        lastLoVal = ZigZagLo[0];
                        lastLoBar = CurrentBar;

                        if (Plots[0].Brush != Brushes.Transparent)
                        {
                            Draw.Line(this, lastHiBar.ToString(), CurrentBar - lastHiBar, lastHiVal, CurrentBar - lastLoBar, lastLoVal, Plots[0].Brush);
                        }

                        ZigZagDots[CurrentBar - lastLoBar] = lastLoVal;
                    }
                    else if (ZigZagLo[0] > lastLoVal && ZigZagHi[0] > ZigZagHi[1])
                    {
                        if (Plots[0].Brush != Brushes.Transparent)
                        {
                            Draw.Line(this, lastHiBar.ToString(), CurrentBar - lastHiBar, lastHiVal, CurrentBar - lastLoBar, lastLoVal, Plots[0].Brush);
                        }

                        ZigZagDots[CurrentBar - lastLoBar] = lastLoVal;

                        zzDir = 1;
                        lastHiVal = ZigZagHi[0];
                        lastHiBar = CurrentBar;

                        if (Plots[0].Brush != Brushes.Transparent)
                        {
                            Draw.Line(this, lastLoBar.ToString(), CurrentBar - lastLoBar, lastLoVal, CurrentBar - lastHiBar, lastHiVal, Plots[0].Brush);
                        }

                        ZigZagDots[CurrentBar - lastHiBar] = lastHiVal;
                    }
                }
            }
        }

        #region Properties

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> ZigZagDots
        {
            get { return Values[0]; }
        }

        /// ---

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Span", Order = 1, GroupName = "Parameters")]
        public int zzSpan
        { get; set; }

        #endregion
    }
}