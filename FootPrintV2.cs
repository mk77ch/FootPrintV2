#region Using declarations
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Timers;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Globalization;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;
using SharpDX.DirectWrite;
using System.Diagnostics;
#endregion

namespace NinjaTrader.NinjaScript.Indicators.Infinity
{
	#region CategoryOrder

	[Gui.CategoryOrder("General", 1)]
	[Gui.CategoryOrder("Profile (Previous Session)", 2)]
	[Gui.CategoryOrder("Profile (Current Session)", 3)]
	[Gui.CategoryOrder("Profile (Custom)", 4)]
	[Gui.CategoryOrder("Footprint", 5)]
	[Gui.CategoryOrder("Bottom Area", 6)]
	[Gui.CategoryOrder("Bottom Text", 7)]
	[Gui.CategoryOrder("Tape Strip", 8)]
	[Gui.CategoryOrder("Depth Profile", 9)]
	[Gui.CategoryOrder("Midas", 10)]
	[Gui.CategoryOrder("Colors", 11)]
	[Gui.CategoryOrder("Max Opacity", 12)]
	[Gui.CategoryOrder("Hotkeys", 13)]
	[Gui.CategoryOrder("Signals", 14)]
	[Gui.CategoryOrder("Alerts", 15)]
	[Gui.CategoryOrder("Misc", 16)]

	#endregion

	#region PatternDetector

	public class DetectedPattern
	{
		public int BarIndex { get; set; }
		public double Price { get; set; }
		public string PatternType { get; set; } = "Unknown";
		public int Direction { get; set; }
		public string Details { get; set; } = "";
	}
	public class PatternDetector
	{
		private readonly BarData barData;
		private readonly double tickSize;
		private readonly int lookback = 10;
		public List<DetectedPattern> DetectedPatterns = new List<DetectedPattern>();

		public PatternDetector(BarData barData, double tickSize)
		{
			this.barData = barData;
			this.tickSize = tickSize;
		}
		public void OnBarUpdate(int currentBar)
		{
			if (!barData.BarItems.IsValidDataPointAt(currentBar)) return;
			var barItem = barData.BarItems.GetValueAt(currentBar);
			if (barItem == null || barItem.rowItems == null || barItem.rowItems.Count == 0) return;

			// Calculate average level volume manually to avoid dynamic/LINQ issues
			double totalVolume = 0;
			int count = 0;
			foreach (var rowItem in barItem.rowItems.Values)
			{
				totalVolume += rowItem.ask + rowItem.bid;
				count++;
			}
			double avgLevelVolume = count > 0 ? totalVolume / count : 0;

			double avgBarVolume = GetRollingAvgBarVolume(currentBar, lookback);
			double avgBarDelta = GetRollingAvgBarDelta(currentBar, lookback);

			DetectAbsorption(currentBar, barItem, avgLevelVolume);
			//DetectImbalanceCluster(currentBar, barItem, avgLevelVolume);
			//DetectDeltaDivergence(currentBar, barItem, avgBarDelta);
			//DetectZeroPrints(currentBar, barItem);
			//DetectInitiatingResponsive(currentBar, barItem, avgLevelVolume);
			//DetectFootprintTails(currentBar, barItem, avgLevelVolume);
			//DetectMultiBarAbsAgg(currentBar, barItem, avgLevelVolume, avgBarDelta);
		}

		// ---- Pattern Methods ----

		private void DetectAbsorption(int bar, dynamic barItem, double avgLevelVolume)
		{
			try
			{
				double absorptionThreshold = avgLevelVolume * 2.0;

				foreach (var row in barItem.rowItems)
				{
					if (row.Value.ask > absorptionThreshold && barItem.max == row.Key)
					{
						DetectedPatterns.Add(new DetectedPattern { BarIndex = bar, Price = row.Key, PatternType = "Absorption-Ask", Direction = 1, Details = "Absorption at high" });
					}
					if (row.Value.bid > absorptionThreshold && barItem.min == row.Key)
					{
						DetectedPatterns.Add(new DetectedPattern { BarIndex = bar, Price = row.Key, PatternType = "Absorption-Bid", Direction = -1, Details = "Absorption at low" });
					}
				}
			}
			catch (Exception ex)
			{ }
		}

		private void DetectImbalanceCluster(int bar, dynamic barItem, double avgLevelVolume)
		{
			double minImbalanceRatio = 0.4; // You can make this configurable
			int minClusterLength = 3; // Configurable
			int streakAsk = 0, streakBid = 0;
			var prices = ((IEnumerable<double>)barItem.rowItems.Keys).OrderByDescending(x => x).ToList(); foreach (var price in prices)
			{
				if (barItem.isAskImbalance(price, price - tickSize, minImbalanceRatio))
				{
					streakAsk++;
					if (streakAsk >= minClusterLength)
						DetectedPatterns.Add(new DetectedPattern { BarIndex = bar, Price = price, PatternType = "ImbalanceCluster-Ask", Direction = 1, Details = "Ask imbalance cluster" });
				}
				else streakAsk = 0;

				if (barItem.isBidImbalance(price + tickSize, price, minImbalanceRatio))
				{
					streakBid++;
					if (streakBid >= minClusterLength)
						DetectedPatterns.Add(new DetectedPattern { BarIndex = bar, Price = price, PatternType = "ImbalanceCluster-Bid", Direction = -1, Details = "Bid imbalance cluster" });
				}
				else streakBid = 0;
			}
		}

		private void DetectDeltaDivergence(int bar, dynamic barItem, double avgBarDelta)
		{
			// Needs access to swing highs/lows (ZigZag). Here we use delta lower (divergence) at new high, higher at new low.
			// To keep this example simple, let's just compare current delta to rolling avg.
			if (barItem.dtc > 0 && barItem.dtc < avgBarDelta)
				DetectedPatterns.Add(new DetectedPattern { BarIndex = bar, Price = barItem.max, PatternType = "DeltaDivergence-High", Direction = 1, Details = "Weak delta at high" });
			if (barItem.dtc < 0 && Math.Abs(barItem.dtc) < Math.Abs(avgBarDelta))
				DetectedPatterns.Add(new DetectedPattern { BarIndex = bar, Price = barItem.min, PatternType = "DeltaDivergence-Low", Direction = -1, Details = "Weak delta at low" });
		}

		private void DetectZeroPrints(int bar, dynamic barItem)
		{
			var highRow = barItem.rowItems[barItem.max];
			if (highRow.ask > 0 && highRow.bid == 0)
				DetectedPatterns.Add(new DetectedPattern { BarIndex = bar, Price = barItem.max, PatternType = "ZeroPrint-High", Direction = 1, Details = "Zero bid at high" });

			var lowRow = barItem.rowItems[barItem.min];
			if (lowRow.bid > 0 && lowRow.ask == 0)
				DetectedPatterns.Add(new DetectedPattern { BarIndex = bar, Price = barItem.min, PatternType = "ZeroPrint-Low", Direction = -1, Details = "Zero ask at low" });
		}

		private void DetectInitiatingResponsive(int bar, dynamic barItem, double avgLevelVolume)
		{
			double threshold = avgLevelVolume * 2.0;
			var openRow = barItem.rowItems.ContainsKey(barItem.opn) ? barItem.rowItems[barItem.opn] : null;
			var closeRow = barItem.rowItems.ContainsKey(barItem.cls) ? barItem.rowItems[barItem.cls] : null;
			if (openRow != null && openRow.ask > threshold)
				DetectedPatterns.Add(new DetectedPattern { BarIndex = bar, Price = barItem.opn, PatternType = "InitiatingBuy-Open", Direction = 1, Details = "Strong buy at open" });
			if (closeRow != null && closeRow.bid > threshold)
				DetectedPatterns.Add(new DetectedPattern { BarIndex = bar, Price = barItem.cls, PatternType = "InitiatingSell-Close", Direction = -1, Details = "Strong sell at close" });
		}

		private void DetectFootprintTails(int bar, dynamic barItem, double avgLevelVolume)
		{
			int tailRows = 2;
			double sumBid = 0, sumAsk = 0;
			var prices = ((IEnumerable<double>)barItem.rowItems.Keys).OrderBy(x => x).ToList(); // ascending for low tail
			for (int i = 0; i < Math.Min(tailRows, prices.Count); i++)
			{
				var row = barItem.rowItems[prices[i]];
				sumBid += row.bid;
				sumAsk += row.ask;
			}
			if (sumBid > avgLevelVolume * tailRows)
				DetectedPatterns.Add(new DetectedPattern { BarIndex = bar, Price = prices[0], PatternType = "BuyingTail", Direction = 1, Details = "Strong buying tail" });

			// Top tail
			sumBid = 0; sumAsk = 0;
			prices.Reverse();
			for (int i = 0; i < Math.Min(tailRows, prices.Count); i++)
			{
				var row = barItem.rowItems[prices[i]];
				sumBid += row.bid;
				sumAsk += row.ask;
			}
			if (sumAsk > avgLevelVolume * tailRows)
				DetectedPatterns.Add(new DetectedPattern { BarIndex = bar, Price = prices[0], PatternType = "SellingTail", Direction = -1, Details = "Strong selling tail" });
		}

		private void DetectMultiBarAbsAgg(int bar, dynamic barItem, double avgLevelVolume, double avgBarDelta)
		{
			// Example: if previous bar had absorption at high, and this bar trades through with high delta
			// For simplicity, just look one bar back
			if (bar < 1) return;
			var prevBarItem = barData.BarItems.IsValidDataPointAt(bar - 1) ? barData.BarItems.GetValueAt(bar - 1) : null;
			if (prevBarItem != null)
			{
				var prevHighRow = prevBarItem.rowItems[prevBarItem.max];
				if (prevHighRow.ask > avgLevelVolume * 2.0 && barItem.max > prevBarItem.max && barItem.dtc > avgBarDelta * 1.5)
				{
					DetectedPatterns.Add(new DetectedPattern { BarIndex = bar, Price = barItem.max, PatternType = "AbsAgg-Breakout", Direction = 1, Details = "Aggressive buy after absorption" });
				}
				var prevLowRow = prevBarItem.rowItems[prevBarItem.min];
				if (prevLowRow.bid > avgLevelVolume * 2.0 && barItem.min < prevBarItem.min && barItem.dtc < avgBarDelta * -1.5)
				{
					DetectedPatterns.Add(new DetectedPattern { BarIndex = bar, Price = barItem.min, PatternType = "AbsAgg-Breakdown", Direction = -1, Details = "Aggressive sell after absorption" });
				}
			}
		}

		// ---- Helper Methods ----

		private double GetRollingAvgBarVolume(int currentBar, int N)
		{
			double sum = 0; int count = 0;
			for (int i = 1; i <= N; i++)
			{
				int idx = currentBar - i;
				if (idx < 0 || !barData.BarItems.IsValidDataPointAt(idx)) continue;
				var barItem = barData.BarItems.GetValueAt(idx);
				if (barItem == null) continue;
				sum += barItem.vol;
				count++;
			}
			return count > 0 ? sum / count : 1.0;
		}
		private double GetRollingAvgBarDelta(int currentBar, int N)
		{
			double sum = 0; int count = 0;
			for (int i = 1; i <= N; i++)
			{
				int idx = currentBar - i;
				if (idx < 0 || !barData.BarItems.IsValidDataPointAt(idx)) continue;
				var barItem = barData.BarItems.GetValueAt(idx);
				if (barItem == null) continue;
				sum += barItem.dtc;  // Fixed: Changed from 'dta' to 'dtc'
				count++;
			}
			return count > 0 ? sum / count : 1.0;
		}
	}

	#endregion

	public class FootPrintV2 : Indicator
	{
		#region cdArea

		public class cdArea
		{
			public int cdFr = 0;
			public int cdTo = 0;
			public double cdHi = 0.0;
			public double cdLo = 0.0;

			public cdArea(int cdFr, int cdTo, double cdHi, double cdLo)
			{
				this.cdFr = cdFr;
				this.cdTo = cdTo;
				this.cdHi = cdHi;
				this.cdLo = cdLo;
			}
		}

		#endregion

		#region TapeStripItem

		public class TapeStripItem
		{
			public int dir = 0;
			public double prc = 0.0;
			public double vol = 0.0;

			public TapeStripItem(int dir, double prc, double vol)
			{
				this.dir = dir;
				this.prc = prc;
				this.vol = vol;
			}
		}

		#endregion

		#region MidasItem

		public class MidasItem
		{
			public int dir = 0;
			public int bar = 0;
			public double prc = 0.0;
			public bool run = true;
			public int lst = 0;

			public List<double> vwap = new List<double>();

			public MidasItem(int dir, int bar, double prc)
			{
				this.dir = dir;
				this.bar = bar;
				this.prc = prc;
			}
		}

		#endregion

		#region StackedImbalanceItem

		public class StackedImbalanceItem
		{
			public int dir = 0;
			public int bar = 0;
			public int end = 0;
			public double max = 0.0;
			public double min = 0.0;

			public StackedImbalanceItem(int dir, int bar, double max, double min)
			{
				this.dir = dir;
				this.bar = bar;
				this.max = max;
				this.min = min;
			}
		}

		#endregion

		#region DepthRow

		public class DepthRow
		{
			public double prc;
			public long vol;
		}

		#endregion

		#region Variables

		private bool log = true;
		private bool bor = false;

		private BarData barData;

		private List<TapeStripItem> TapeStripItems = new List<TapeStripItem>();

		private List<MidasItem> MidasItems = new List<MidasItem>();

		private List<DepthRow> AskDepthRows = new List<DepthRow>();
		private List<DepthRow> BidDepthRows = new List<DepthRow>();

		private List<StackedImbalanceItem> StackedImbalanceItems = new List<StackedImbalanceItem>();

		private double dTextSize = 0;
		private int barFullWidth = 0;
		private int barHalfWidth = 0;
		private int cellWidth = 0;
		private bool forceRefresh = false;

		SimpleFont sfMini;
		SimpleFont sfNorm;
		SimpleFont sfBold;

		TextFormat tfMini;
		TextFormat tfNorm;
		TextFormat tfBold;

		Stroke dLine = new Stroke(Brushes.Gray, DashStyleHelper.Dot, 1f);

		/// zz

		private Series<double> ZigZagLo;
		private Series<double> ZigZagHi;

		private int zzSpan = 2;
		private int zzDir = 0;
		private int lastLoBar = 0;
		private int lastHiBar = 0;
		private double lastLoVal = 0.0;
		private double lastHiVal = 0.0;

		public PatternDetector patternDetector;
		private PatternTooltipHelper tooltipHelper;

		/// menu

		private NinjaTrader.Gui.Chart.ChartTab chartTab;
		private NinjaTrader.Gui.Chart.Chart chartWindow;
		private bool isToolBarButtonAdded;
		private System.Windows.DependencyObject searchObject;
		private System.Windows.Controls.TabItem tabItem;
		private System.Windows.Controls.Menu theMenu;
		private string theMenuAutomationID;
		private NinjaTrader.Gui.Tools.NTMenuItem topMenuItem1;
		private NinjaTrader.Gui.Tools.NTMenuItem topMenuItem2;
		private NinjaTrader.Gui.Tools.NTMenuItem topMenuItem2SubItem1;
		private NinjaTrader.Gui.Tools.NTMenuItem topMenuItem2SubItem2;
		private NinjaTrader.Gui.Tools.NTMenuItem topMenuItem3;
		private NinjaTrader.Gui.Tools.NTMenuItem topMenuItem3SubItem1;
		private NinjaTrader.Gui.Tools.NTMenuItem topMenuItem3SubItem2;
		private NinjaTrader.Gui.Tools.NTMenuItem topMenuItem3SubItem3;
		private NinjaTrader.Gui.Tools.NTMenuItem topMenuItem3SubItem4;
		private NinjaTrader.Gui.Tools.NTMenuItem topMenuItem3SubItem5;
		private NinjaTrader.Gui.Tools.NTMenuItem topMenuItem3SubItem6;
		private NinjaTrader.Gui.Tools.NTMenuItem topMenuItem3SubItem7;
		private NinjaTrader.Gui.Tools.NTMenuItem topMenuItem3SubItem8;
		private NinjaTrader.Gui.Tools.NTMenuItem topMenuItem3SubItem9;
		private NinjaTrader.Gui.Tools.NTMenuItem topMenuItem3SubItem10;
		private NinjaTrader.Gui.Tools.NTMenuItem topMenuItem3SubItem11;

		/// ---

		private bool brushesInitialized = false;

		private SolidColorBrush bckColor;

		private SharpDX.Direct2D1.Brush bckBrush;
		private SharpDX.Direct2D1.Brush mapBrush;
		private SharpDX.Direct2D1.Brush pocBrush;
		private SharpDX.Direct2D1.Brush proBrush;
		private SharpDX.Direct2D1.Brush ntlBrush;
		private SharpDX.Direct2D1.Brush askBrush;
		private SharpDX.Direct2D1.Brush bidBrush;
		private SharpDX.Direct2D1.Brush stkBrush;
		private SharpDX.Direct2D1.Brush dupBrush;
		private SharpDX.Direct2D1.Brush ddnBrush;

		public ChartScale cScale;

		#endregion

		// methods

		#region OnStateChange

		/// OnStateChange
		///
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description = @"";
				Name = "FootPrintV2";
				Calculate = Calculate.OnEachTick;
				IsOverlay = true;
				IsAutoScale = false;
				DisplayInDataBox = false;
				DrawOnPricePanel = true;
				DrawHorizontalGridLines = true;
				DrawVerticalGridLines = false;
				PaintPriceMarkers = false;
				ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
				IsSuspendedWhileInactive = false;
				MaximumBarsLookBack = MaximumBarsLookBack.Infinite;

				/// ---

				rightMargin = 0;
				leftMargin = 0;
				showClose = true;
				paintBars = true;
				paintBarsByDelta = true;
				showStackedImbalances = false;
				showPocOnBarChart = true;

				/// ---

				prevProfileShow = true;
				prevProfileWidth = 0;
				prevProfileGradient = true;
				prevProfileBar = true;
				prevProfileValueArea = true;
				prevProfileExtendVa = true;
				prevProfileShowDelta = false;
				prevProfileShowInfo = false;

				/// ---

				currProfileShow = true;
				currProfileWidth = 0;
				currProfileGradient = true;
				currProfileBar = true;
				currProfileValueArea = false;
				currProfileExtendVa = false;
				currProfileShowDelta = false;
				currProfileShowInfo = false;

				/// ---

				custProfileShow = true;
				custProfileWidth = 80;
				custProfileGradient = true;
				custProfileValueArea = false;
				custProfileExtendVa = false;
				custProfileShowDelta = true;
				currProfileShowInfo = true;
				custProfilePctValue = 1;
				custProfileBarValue = 8;
				custProfileVolValue = 1;
				custProfileRngValue = 1;
				custProfileMap = true;
				custProfileMapType = FPV2MapDisplayType.Volume;

				/// ---

				showFootprint = false;
				footprintDisplayType = FPV2FootprintDisplayType.Profile;
				footprintDeltaOutline = true;
				footprintDeltaProfile = true;
				footprintDeltaGradient = false;
				footprintImbalances = false;
				minImbalanceRatio = 0.4;
				footprintGradient = false;
				footprintRelativeVolume = false;
				footprintBarVolume = false;
				footprintBarDelta = false;
				footprintBarDeltaSwing = false;

				/// ---

				showBottomArea = true;
				bottomAreaType = FPV2BottomAreaType.Delta;
				bottomAreaGradient = false;
				bottomAreaLabel = true;

				/// ---

				bottomTextDelta = false;
				bottomTextVolume = false;
				bottomTextCumulativeDelta = false;

				/// ---

				showTapeStrip = true;
				tapeStripMaxItems = 5;
				tapeStripFilter = 100;

				/// ---

				showDepth = false;
				depthWidth = 40;
				depthGradient = true;

				/// ---

				showMidas = false;
				midasOpacity = 0.5f;
				midasLineWidth = 1;
				onlyActive = true;

				/// ---

				bullishColor = Brushes.LightGreen;
				bearishColor = Brushes.LightCoral;
				neutralColor = Brushes.White;
				profileColor = Brushes.Gainsboro;
				mapColor = Brushes.DodgerBlue;
				pocColor = Brushes.Violet;
				stackedColor = Brushes.Yellow;
				depthAskColor = Brushes.LightCoral;
				depthBidColor = Brushes.LightGreen;

				/// ---

				profileMaxOpa = 0.3f;
				mapMaxOpa = 0.6f;
				footprintMaxOpa = 0.4f;
				depthMaxOpa = 0.6f;
				stackedImbOpa = 0.4f;

				/// ---

				footprintHotKey = FPV2Hotkeys.None;
				mapHotKey = FPV2Hotkeys.None;

				/// ---

				bullishStackedImbalanceAlert = false;
				bullishStackedImbalanceSound = "";
				bearishStackedImbalanceAlert = false;
				bearishStackedImbalanceSound = "";

				/// ---

				rBarWidth = 3.0;
				rBarDistance = 9f;
				rScaleFixed = false;
				rScaleMax = 0.0;
				rScaleMin = 0.0;
				fBarWidth = 1.0;
				fBarDistance = 67f;
				fScaleFixed = false;
				fScaleMax = 0.0;
				fScaleMin = 0.0;

				/// ---

				AddPlot(new Stroke(Brushes.Transparent, 3f), PlotStyle.Dot, "ZigZagDots");
			}
			else if (State == State.Configure)
			{
				barData = BarData(prevProfileShow, currProfileShow, custProfileShow, custProfilePctValue, custProfileBarValue, custProfileVolValue, custProfileRngValue);
			}
			else if (State == State.DataLoaded)
			{
				if (ChartControl != null && !isToolBarButtonAdded)
				{
					ChartControl.Dispatcher.InvokeAsync((Action)(() =>
					{
						InsertWPFControls();
					}));
				}

				/// ---

				if (ChartControl != null && ChartBars != null)
				{
					sfMini = new SimpleFont("Consolas", ChartControl.Properties.LabelFont.Size * 0.8);
					sfNorm = new SimpleFont("Consolas", ChartControl.Properties.LabelFont.Size);
					sfBold = new SimpleFont("Consolas", ChartControl.Properties.LabelFont.Size) { Bold = true };

					tfMini = sfMini.ToDirectWriteTextFormat();
					tfNorm = sfNorm.ToDirectWriteTextFormat();
					tfBold = sfNorm.ToDirectWriteTextFormat();
				}

				/// ---

				ZigZagLo = new Series<double>(this);
				ZigZagHi = new Series<double>(this);

				/// ---

				if (ChartControl != null)
				{
					ChartControl.KeyDown += chartControlOnKeyDown;
				}

				/// ---

				patternDetector = new PatternDetector(barData, TickSize);

				/// ---

				ChartControl.Dispatcher.InvokeAsync((Action)(() =>
				{
					tooltipHelper = new PatternTooltipHelper(this);
					tooltipHelper.Attach();
				}));

				/// ---

				if (log)
				{
					ClearOutputWindow();
				}
			}
			else if (State == State.Historical)
			{
				SetZOrder(-1);
			}
			else if (State == State.Terminated)
			{
				if (ChartControl != null)
				{
					ChartControl.Dispatcher.InvokeAsync((Action)(() =>
					{
						RemoveWPFControls();
					}));
				}

				/// ---

				if (ChartControl != null)
				{
					ChartControl.KeyDown -= chartControlOnKeyDown;
				}

				/// ---

				if (sfMini != null) sfMini = null;
				if (sfNorm != null) sfNorm = null;
				if (sfBold != null) sfBold = null;

				if (tfMini != null) tfMini.Dispose();
				if (tfNorm != null) tfNorm.Dispose();
				if (tfBold != null) tfBold.Dispose();

				/// ---

				if (brushesInitialized)
				{
					bckColor = null;
					bckBrush.Dispose();
					mapBrush.Dispose();
					pocBrush.Dispose();
					proBrush.Dispose();
					ntlBrush.Dispose();
					askBrush.Dispose();
					bidBrush.Dispose();
					stkBrush.Dispose();
					dupBrush.Dispose();
					ddnBrush.Dispose();
				}

				/// ---

				TapeStripItems.Clear();
				MidasItems.Clear();
				AskDepthRows.Clear();
				BidDepthRows.Clear();
				StackedImbalanceItems.Clear();

				/// ---

				if (patternDetector != null)
				{
					patternDetector.DetectedPatterns.Clear();
					patternDetector = null;
				}

				if (tooltipHelper != null) tooltipHelper.Detach();
			}
		}

		#endregion

		#region chartPanelOnKeyDown

		public void chartControlOnKeyDown(object sender, KeyEventArgs e)
		{
			if ((Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) && e.Key == Key.Space)
			{
				if (footprintHotKey == FPV2Hotkeys.ShiftSpace)
				{
					toggleFootprint();
				}
				if (custProfileShow && mapHotKey == FPV2Hotkeys.ShiftSpace)
				{
					toggleMap();
				}
			}
			else if ((Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) && e.Key == Key.Space)
			{
				if (footprintHotKey == FPV2Hotkeys.CtrlSpace)
				{
					toggleFootprint();
				}
				if (custProfileShow && mapHotKey == FPV2Hotkeys.CtrlSpace)
				{
					toggleMap();
				}
			}
			else if (custProfileMap)
			{
				if (e.Key == Key.Space)
				{
					if (custProfileMapType == FPV2MapDisplayType.Volume)
					{
						custProfileMapType = FPV2MapDisplayType.Delta;
						topMenuItem2SubItem2.Header = "Switch to Volume";
					}
					else
					{
						custProfileMapType = FPV2MapDisplayType.Volume;
						topMenuItem2SubItem2.Header = "Switch to Delta";
					}

					refreshChart();
				}
			}
		}

		#endregion

		#region toggleFootprint

		private void toggleFootprint()
		{
			if (showFootprint)
			{
				showFootprint = false;
				topMenuItem3SubItem1.Header = "Show";

				fBarWidth = ChartControl.BarWidth;
				fBarDistance = ChartControl.Properties.BarDistance;
				fScaleFixed = (cScale.Properties.YAxisRangeType == YAxisRangeType.Fixed) ? true : false;
				fScaleMin = cScale.Properties.FixedScaleMin;
				fScaleMax = cScale.Properties.FixedScaleMax;

				ChartControl.BarWidth = rBarWidth;
				ChartControl.Properties.BarDistance = rBarDistance;

				if (rScaleFixed)
				{
					cScale.Properties.YAxisRangeType = YAxisRangeType.Fixed;
					cScale.Properties.FixedScaleMin = rScaleMin;
					cScale.Properties.FixedScaleMax = rScaleMax;
				}
				else
				{
					cScale.Properties.YAxisRangeType = YAxisRangeType.Automatic;
					cScale.Properties.FixedScaleMin = 0.0;
					cScale.Properties.FixedScaleMax = 0.0;
				}
			}
			else
			{
				showFootprint = true;
				topMenuItem3SubItem1.Header = "Hide";

				rBarWidth = ChartControl.BarWidth;
				rBarDistance = ChartControl.Properties.BarDistance;
				rScaleFixed = (cScale.Properties.YAxisRangeType == YAxisRangeType.Fixed) ? true : false;
				rScaleMin = cScale.Properties.FixedScaleMin;
				rScaleMax = cScale.Properties.FixedScaleMax;

				ChartControl.BarWidth = fBarWidth;
				ChartControl.Properties.BarDistance = fBarDistance;

				if (fScaleFixed)
				{
					cScale.Properties.YAxisRangeType = YAxisRangeType.Fixed;
					cScale.Properties.FixedScaleMin = fScaleMin;
					cScale.Properties.FixedScaleMax = fScaleMax;
				}
				else
				{
					cScale.Properties.YAxisRangeType = YAxisRangeType.Automatic;
					cScale.Properties.FixedScaleMin = 0.0;
					cScale.Properties.FixedScaleMax = 0.0;
				}
			}

			topMenuItem3SubItem2.IsEnabled = showFootprint;
			topMenuItem3SubItem3.IsEnabled = showFootprint;
			topMenuItem3SubItem4.IsEnabled = showFootprint;
			topMenuItem3SubItem5.IsEnabled = showFootprint;
			topMenuItem3SubItem6.IsEnabled = showFootprint;
			topMenuItem3SubItem7.IsEnabled = showFootprint;
			topMenuItem3SubItem8.IsEnabled = showFootprint;
			topMenuItem3SubItem9.IsEnabled = showFootprint;
			topMenuItem3SubItem10.IsEnabled = showFootprint;
			topMenuItem3SubItem11.IsEnabled = showFootprint;

			// ---

			NTWindow ntWindow = (NTWindow)Window.GetWindow(ChartControl.Parent);
			System.Windows.Controls.TabControl tabControl = ntWindow.FindFirst("ChartWindowTabControl") as System.Windows.Controls.TabControl;

			foreach (System.Windows.Controls.TabItem tabItem in tabControl.Items)
			{
				ChartControl tabChartControl = (tabItem.Content as ChartTab).ChartControl;

				if (tabItem.IsSelected)
				{
					if (ScaleJustification == ScaleJustification.Left)
					{
						try
						{
							System.Windows.Controls.Button fixedScaleButton = tabChartControl.Children.OfType<System.Windows.Controls.Button>().Where(b => b.Content.ToString() == "F").First();

							fixedScaleButton.Visibility = (cScale.Properties.YAxisRangeType == YAxisRangeType.Fixed) ? Visibility.Visible : Visibility.Hidden;
						}
						catch (Exception e) { }
					}
					if (ScaleJustification == ScaleJustification.Right)
					{
						try
						{
							System.Windows.Controls.Button fixedScaleButton = tabChartControl.Children.OfType<System.Windows.Controls.Button>().Where(b => b.Content.ToString() == "F").Last();

							fixedScaleButton.Visibility = (cScale.Properties.YAxisRangeType == YAxisRangeType.Fixed) ? Visibility.Visible : Visibility.Hidden;
						}
						catch (Exception e) { }
					}
				}
			}

			// ---

			refreshChart();

			forceRefresh = true;
		}

		#endregion

		#region toggleMap

		private void toggleMap()
		{
			custProfileMap = !custProfileMap;
			topMenuItem2SubItem1.Header = (custProfileMap) ? "Hide" : "Show";

			topMenuItem2SubItem2.IsEnabled = custProfileMap;

			refreshChart();
		}

		#endregion

		#region Menu

		protected void InsertWPFControls()
		{
			chartWindow = System.Windows.Window.GetWindow(ChartControl.Parent) as Chart;

			theMenuAutomationID = string.Format("ChartToolbarToggleTrades{0}", DateTime.Now.ToString("yyMMddhhmmss"));

			foreach (System.Windows.DependencyObject item in chartWindow.MainMenu)
			{
				if (System.Windows.Automation.AutomationProperties.GetAutomationId(item) == theMenuAutomationID)
				{
					return;
				}
			}

			theMenu = new System.Windows.Controls.Menu
			{
				VerticalAlignment = VerticalAlignment.Center,
				VerticalContentAlignment = VerticalAlignment.Center,
				Style = System.Windows.Application.Current.TryFindResource("SystemMenuStyle") as Style
			};

			System.Windows.Automation.AutomationProperties.SetAutomationId(theMenu, theMenuAutomationID);

			System.Windows.Media.Geometry topMenuItem1Icon = System.Windows.Media.Geometry.Parse("M19.5,3.09L15,7.59V4H13V11H20V9H16.41L20.91,4.5L19.5,3.09M4,13V15H7.59L3.09,19.5L4.5,20.91L9,16.41V20H11V13H4Z");
			System.Windows.Media.Geometry topMenuItem2Icon = System.Windows.Media.Geometry.Parse("M2,2H4V20H22V22H2V2M7,10H17V13H7V10M11,15H21V18H11V15M6,4H22V8H20V6H8V8H6V4Z");
			System.Windows.Media.Geometry topMenuItem3Icon = System.Windows.Media.Geometry.Parse("M2,5H10V2H12V22H10V18H6V15H10V13H4V10H10V8H2V5M14,5H17V8H14V5M14,10H19V13H14V10M14,15H22V18H14V15Z");

			/// Trades

			topMenuItem1 = new Gui.Tools.NTMenuItem()
			{
				Header = "Trades",
				Foreground = (ChartControl.BarsArray[0].Properties.PlotExecutions == ChartExecutionStyle.DoNotPlot) ? Brushes.DimGray : Brushes.Silver,
				Icon = topMenuItem1Icon,
				Margin = new System.Windows.Thickness(0),
				Padding = new System.Windows.Thickness(1),
				VerticalAlignment = VerticalAlignment.Center,
				FontSize = 11,
				Style = System.Windows.Application.Current.TryFindResource("MainMenuItem") as Style
			};

			topMenuItem1.Click += topMenuItem1_Click;
			theMenu.Items.Add(topMenuItem1);

			/// Map

			topMenuItem2 = new Gui.Tools.NTMenuItem()
			{
				Header = "Map",
				Foreground = Brushes.Silver,
				Icon = topMenuItem2Icon,
				Margin = new System.Windows.Thickness(0),
				Padding = new System.Windows.Thickness(1),
				VerticalAlignment = VerticalAlignment.Center,
				FontSize = 11,
				Style = System.Windows.Application.Current.TryFindResource("MainMenuItem") as Style,
				Visibility = (!custProfileShow) ? Visibility.Collapsed : Visibility.Visible
			};

			theMenu.Items.Add(topMenuItem2);

			topMenuItem2SubItem1 = new Gui.Tools.NTMenuItem()
			{
				Header = (custProfileMap) ? "Hide" : "Show",
				InputGestureText = (mapHotKey == FPV2Hotkeys.ShiftSpace) ? "Shift+Space" : (mapHotKey == FPV2Hotkeys.CtrlSpace) ? "Ctrl+Space" : "",
				Foreground = Brushes.Silver,
				Margin = new System.Windows.Thickness(0),
				Padding = new System.Windows.Thickness(1),
				VerticalAlignment = VerticalAlignment.Center,
				FontSize = 11,
				Style = System.Windows.Application.Current.TryFindResource("MainMenuItem") as Style
			};

			topMenuItem2SubItem1.Click += topMenuItem2SubItem1_Click;
			topMenuItem2.Items.Add(topMenuItem2SubItem1);

			topMenuItem2SubItem2 = new Gui.Tools.NTMenuItem()
			{
				Header = (custProfileMapType == FPV2MapDisplayType.Volume) ? "Switch to Delta" : "Switch to Volume",
				InputGestureText = "Space",
				Foreground = Brushes.Silver,
				Margin = new System.Windows.Thickness(0),
				Padding = new System.Windows.Thickness(1),
				VerticalAlignment = VerticalAlignment.Center,
				FontSize = 11,
				Style = System.Windows.Application.Current.TryFindResource("MainMenuItem") as Style,
				IsEnabled = custProfileMap
			};

			topMenuItem2SubItem2.Click += topMenuItem2SubItem2_Click;
			topMenuItem2.Items.Add(topMenuItem2SubItem2);

			/// Footprint

			topMenuItem3 = new Gui.Tools.NTMenuItem()
			{
				Header = "Footprint",
				Foreground = Brushes.Silver,
				Icon = topMenuItem3Icon,
				Margin = new System.Windows.Thickness(0),
				Padding = new System.Windows.Thickness(1),
				VerticalAlignment = VerticalAlignment.Center,
				FontSize = 11,
				Style = System.Windows.Application.Current.TryFindResource("MainMenuItem") as Style
			};

			theMenu.Items.Add(topMenuItem3);

			topMenuItem3SubItem1 = new Gui.Tools.NTMenuItem()
			{
				Header = (showFootprint) ? "Hide" : "Show",
				InputGestureText = (footprintHotKey == FPV2Hotkeys.ShiftSpace) ? "Shift+Space" : (footprintHotKey == FPV2Hotkeys.CtrlSpace) ? "Ctrl+Space" : "",
				Foreground = Brushes.Silver,
				Margin = new System.Windows.Thickness(0),
				Padding = new System.Windows.Thickness(1),
				VerticalAlignment = VerticalAlignment.Center,
				FontSize = 11,
				Style = System.Windows.Application.Current.TryFindResource("MainMenuItem") as Style,
			};

			topMenuItem3SubItem1.Click += topMenuItem3SubItem1_Click;
			topMenuItem3.Items.Add(topMenuItem3SubItem1);

			topMenuItem3SubItem2 = new Gui.Tools.NTMenuItem()
			{
				Header = (footprintDisplayType == FPV2FootprintDisplayType.Profile) ? "Switch to Numbers" : "Switch to Profile",
				Foreground = Brushes.Silver,
				Margin = new System.Windows.Thickness(0),
				Padding = new System.Windows.Thickness(1),
				VerticalAlignment = VerticalAlignment.Center,
				FontSize = 11,
				Style = System.Windows.Application.Current.TryFindResource("MainMenuItem") as Style,
				IsEnabled = showFootprint
			};

			topMenuItem3SubItem2.Click += topMenuItem3SubItem2_Click;
			topMenuItem3.Items.Add(topMenuItem3SubItem2);

			topMenuItem3SubItem3 = new Gui.Tools.NTMenuItem()
			{
				Header = "Imbalances",
				Foreground = Brushes.Silver,
				Margin = new System.Windows.Thickness(0),
				Padding = new System.Windows.Thickness(1),
				VerticalAlignment = VerticalAlignment.Center,
				FontSize = 11,
				Style = System.Windows.Application.Current.TryFindResource("MainMenuItem") as Style,
				IsEnabled = showFootprint,
				IsCheckable = true,
				IsChecked = footprintImbalances
			};

			topMenuItem3SubItem3.Click += topMenuItem3SubItem3_Click;
			topMenuItem3.Items.Add(topMenuItem3SubItem3);

			topMenuItem3SubItem4 = new Gui.Tools.NTMenuItem()
			{
				Header = "Delta Outline",
				Foreground = Brushes.Silver,
				Margin = new System.Windows.Thickness(0),
				Padding = new System.Windows.Thickness(1),
				VerticalAlignment = VerticalAlignment.Center,
				FontSize = 11,
				Style = System.Windows.Application.Current.TryFindResource("MainMenuItem") as Style,
				IsEnabled = showFootprint,
				IsCheckable = true,
				IsChecked = footprintDeltaOutline
			};

			topMenuItem3SubItem4.Click += topMenuItem3SubItem4_Click;
			topMenuItem3.Items.Add(topMenuItem3SubItem4);

			topMenuItem3SubItem5 = new Gui.Tools.NTMenuItem()
			{
				Header = "Gradient",
				Foreground = Brushes.Silver,
				Margin = new System.Windows.Thickness(0),
				Padding = new System.Windows.Thickness(1),
				VerticalAlignment = VerticalAlignment.Center,
				FontSize = 11,
				Style = System.Windows.Application.Current.TryFindResource("MainMenuItem") as Style,
				IsEnabled = showFootprint,
				IsCheckable = true,
				IsChecked = footprintGradient
			};

			topMenuItem3SubItem5.Click += topMenuItem3SubItem5_Click;
			topMenuItem3.Items.Add(topMenuItem3SubItem5);

			topMenuItem3SubItem6 = new Gui.Tools.NTMenuItem()
			{
				Header = "Relative Volume",
				Foreground = Brushes.Silver,
				Margin = new System.Windows.Thickness(0),
				Padding = new System.Windows.Thickness(1),
				VerticalAlignment = VerticalAlignment.Center,
				FontSize = 11,
				Style = System.Windows.Application.Current.TryFindResource("MainMenuItem") as Style,
				IsEnabled = showFootprint,
				IsCheckable = true,
				IsChecked = footprintRelativeVolume
			};

			topMenuItem3SubItem6.Click += topMenuItem3SubItem6_Click;
			topMenuItem3.Items.Add(topMenuItem3SubItem6);

			topMenuItem3SubItem7 = new Gui.Tools.NTMenuItem()
			{
				Header = "Delta Profile",
				Foreground = Brushes.Silver,
				Margin = new System.Windows.Thickness(0),
				Padding = new System.Windows.Thickness(1),
				VerticalAlignment = VerticalAlignment.Center,
				FontSize = 11,
				Style = System.Windows.Application.Current.TryFindResource("MainMenuItem") as Style,
				IsEnabled = showFootprint,
				IsCheckable = true,
				IsChecked = footprintDeltaProfile,
				Visibility = (footprintDisplayType == FPV2FootprintDisplayType.Numbers) ? Visibility.Visible : Visibility.Collapsed
			};

			topMenuItem3SubItem7.Click += topMenuItem3SubItem7_Click;
			topMenuItem3.Items.Add(topMenuItem3SubItem7);

			topMenuItem3SubItem8 = new Gui.Tools.NTMenuItem()
			{
				Header = "Delta Profile Gradient",
				Foreground = Brushes.Silver,
				Margin = new System.Windows.Thickness(0),
				Padding = new System.Windows.Thickness(1),
				VerticalAlignment = VerticalAlignment.Center,
				FontSize = 11,
				Style = System.Windows.Application.Current.TryFindResource("MainMenuItem") as Style,
				IsEnabled = showFootprint,
				IsCheckable = true,
				IsChecked = footprintDeltaGradient,
				Visibility = (footprintDisplayType == FPV2FootprintDisplayType.Numbers) ? Visibility.Visible : Visibility.Collapsed
			};

			topMenuItem3SubItem8.Click += topMenuItem3SubItem8_Click;
			topMenuItem3.Items.Add(topMenuItem3SubItem8);

			topMenuItem3SubItem9 = new Gui.Tools.NTMenuItem()
			{
				Header = "Per Bar Volume",
				Foreground = Brushes.Silver,
				Margin = new System.Windows.Thickness(0),
				Padding = new System.Windows.Thickness(1),
				VerticalAlignment = VerticalAlignment.Center,
				FontSize = 11,
				Style = System.Windows.Application.Current.TryFindResource("MainMenuItem") as Style,
				IsEnabled = showFootprint,
				IsCheckable = true,
				IsChecked = footprintBarVolume,
			};

			topMenuItem3SubItem9.Click += topMenuItem3SubItem9_Click;
			topMenuItem3.Items.Add(topMenuItem3SubItem9);

			topMenuItem3SubItem10 = new Gui.Tools.NTMenuItem()
			{
				Header = "Per Bar Delta",
				Foreground = Brushes.Silver,
				Margin = new System.Windows.Thickness(0),
				Padding = new System.Windows.Thickness(1),
				VerticalAlignment = VerticalAlignment.Center,
				FontSize = 11,
				Style = System.Windows.Application.Current.TryFindResource("MainMenuItem") as Style,
				IsEnabled = showFootprint,
				IsCheckable = true,
				IsChecked = footprintBarDelta,
			};

			topMenuItem3SubItem10.Click += topMenuItem3SubItem10_Click;
			topMenuItem3.Items.Add(topMenuItem3SubItem10);

			topMenuItem3SubItem11 = new Gui.Tools.NTMenuItem()
			{
				Header = "Cumulative Swing Delta",
				Foreground = Brushes.Silver,
				Margin = new System.Windows.Thickness(0),
				Padding = new System.Windows.Thickness(1),
				VerticalAlignment = VerticalAlignment.Center,
				FontSize = 11,
				Style = System.Windows.Application.Current.TryFindResource("MainMenuItem") as Style,
				IsEnabled = showFootprint,
				IsCheckable = true,
				IsChecked = footprintBarDeltaSwing,
			};

			topMenuItem3SubItem11.Click += topMenuItem3SubItem11_Click;
			topMenuItem3.Items.Add(topMenuItem3SubItem11);

			/// ---

			chartWindow.MainMenu.Add(theMenu);

			chartWindow.MainTabControl.SelectionChanged += MySelectionChangedHandler;
		}

		private void MySelectionChangedHandler(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
		{
			if (e.AddedItems.Count <= 0)
			{
				return;
			}

			tabItem = e.AddedItems[0] as System.Windows.Controls.TabItem;

			if (tabItem == null)
			{
				return;
			}

			chartTab = tabItem.Content as NinjaTrader.Gui.Chart.ChartTab;

			if (chartTab != null)
			{
				if (theMenu != null)
				{
					theMenu.Visibility = (chartTab.ChartControl == ChartControl) ? Visibility.Visible : Visibility.Collapsed;
				}
			}
		}

		protected void RemoveWPFControls()
		{
			if (topMenuItem1 != null)
			{
				topMenuItem1.Click -= topMenuItem1_Click;
			}

			if (topMenuItem2SubItem1 != null)
			{
				topMenuItem2SubItem1.Click -= topMenuItem2SubItem1_Click;
			}

			if (topMenuItem2SubItem2 != null)
			{
				topMenuItem2SubItem2.Click -= topMenuItem2SubItem2_Click;
			}

			if (topMenuItem3SubItem1 != null)
			{
				topMenuItem3SubItem1.Click -= topMenuItem3SubItem1_Click;
			}

			if (topMenuItem3SubItem2 != null)
			{
				topMenuItem3SubItem2.Click -= topMenuItem3SubItem2_Click;
			}

			if (topMenuItem3SubItem3 != null)
			{
				topMenuItem3SubItem3.Click -= topMenuItem3SubItem3_Click;
			}

			if (topMenuItem3SubItem4 != null)
			{
				topMenuItem3SubItem4.Click -= topMenuItem3SubItem4_Click;
			}

			if (topMenuItem3SubItem5 != null)
			{
				topMenuItem3SubItem5.Click -= topMenuItem3SubItem5_Click;
			}

			if (topMenuItem3SubItem6 != null)
			{
				topMenuItem3SubItem6.Click -= topMenuItem3SubItem6_Click;
			}

			if (topMenuItem3SubItem7 != null)
			{
				topMenuItem3SubItem7.Click -= topMenuItem3SubItem7_Click;
			}

			if (topMenuItem3SubItem8 != null)
			{
				topMenuItem3SubItem8.Click -= topMenuItem3SubItem8_Click;
			}

			if (topMenuItem3SubItem9 != null)
			{
				topMenuItem3SubItem9.Click -= topMenuItem3SubItem9_Click;
			}

			if (topMenuItem3SubItem10 != null)
			{
				topMenuItem3SubItem10.Click -= topMenuItem3SubItem10_Click;
			}

			if (topMenuItem3SubItem11 != null)
			{
				topMenuItem3SubItem11.Click -= topMenuItem3SubItem11_Click;
			}

			if (theMenu != null)
			{
				chartWindow.MainMenu.Remove(theMenu);
			}

			chartWindow.MainTabControl.SelectionChanged -= MySelectionChangedHandler;
		}

		protected void topMenuItem1_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			if (ChartControl.BarsArray[0].Properties.PlotExecutions != ChartExecutionStyle.DoNotPlot)
			{
				topMenuItem1.Foreground = Brushes.DimGray;

				ChartControl.BarsArray[0].Properties.PlotExecutions = ChartExecutionStyle.DoNotPlot;
			}
			else
			{
				topMenuItem1.Foreground = Brushes.Silver;

				ChartControl.BarsArray[0].Properties.PlotExecutions = ChartExecutionStyle.MarkersOnly;
			}

			ForceRefresh();
		}

		protected void topMenuItem2SubItem1_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			toggleMap();
		}

		protected void topMenuItem2SubItem2_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			custProfileMapType = (custProfileMapType == FPV2MapDisplayType.Volume) ? FPV2MapDisplayType.Delta : FPV2MapDisplayType.Volume;
			topMenuItem2SubItem2.Header = (custProfileMapType == FPV2MapDisplayType.Volume) ? "Switch to Delta" : "Switch to Volume";
			ForceRefresh();
		}

		protected void topMenuItem3SubItem1_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			toggleFootprint();
		}

		protected void topMenuItem3SubItem2_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			footprintDisplayType = (footprintDisplayType == FPV2FootprintDisplayType.Profile) ? FPV2FootprintDisplayType.Numbers : FPV2FootprintDisplayType.Profile;
			topMenuItem3SubItem2.Header = (footprintDisplayType == FPV2FootprintDisplayType.Profile) ? "Switch to Numbers" : "Switch to Profile";

			ForceRefresh();
		}

		protected void topMenuItem3SubItem3_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			footprintImbalances = !footprintImbalances;
			topMenuItem3SubItem3.IsChecked = footprintImbalances;

			ForceRefresh();
		}

		protected void topMenuItem3SubItem4_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			footprintDeltaOutline = !footprintDeltaOutline;
			topMenuItem3SubItem4.IsChecked = footprintDeltaOutline;

			ForceRefresh();
		}

		protected void topMenuItem3SubItem5_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			footprintGradient = !footprintGradient;
			topMenuItem3SubItem5.IsChecked = footprintGradient;

			ForceRefresh();
		}

		protected void topMenuItem3SubItem6_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			footprintRelativeVolume = !footprintRelativeVolume;
			topMenuItem3SubItem6.IsChecked = footprintRelativeVolume;

			ForceRefresh();
		}

		protected void topMenuItem3SubItem7_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			footprintDeltaProfile = !footprintDeltaProfile;
			topMenuItem3SubItem7.IsChecked = footprintDeltaProfile;

			ForceRefresh();
		}

		protected void topMenuItem3SubItem8_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			footprintDeltaGradient = !footprintDeltaGradient;
			topMenuItem3SubItem8.IsChecked = footprintDeltaGradient;

			ForceRefresh();
		}

		protected void topMenuItem3SubItem9_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			footprintBarVolume = !footprintBarVolume;
			topMenuItem3SubItem9.IsChecked = footprintBarVolume;

			ForceRefresh();
		}

		protected void topMenuItem3SubItem10_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			footprintBarDelta = !footprintBarDelta;
			topMenuItem3SubItem10.IsChecked = footprintBarDelta;

			ForceRefresh();
		}

		protected void topMenuItem3SubItem11_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			footprintBarDeltaSwing = !footprintBarDeltaSwing;
			topMenuItem3SubItem11.IsChecked = footprintBarDeltaSwing;

			ForceRefresh();
		}

		#endregion

		#region refreshChart

		private void refreshChart()
		{
			try
			{
				chartWindow.ActiveChartControl.InvalidateVisual();
				ForceRefresh();
			}
			catch (Exception e)
			{
				if (log)
				{
					Print(e.ToString());
				}
			}
		}

		#endregion

		#region DisplayName

		/// DisplayName
		///
		public override string DisplayName
		{
			get { return "FootPrint V2 (" + custProfilePctValue + " / " + custProfileBarValue + " / " + custProfileVolValue + " / " + custProfileRngValue + ")"; }
		}

		#endregion

		#region tapeStrip

		/// tapeStrip
		///
		private void tapeStrip(double prc, double vol, double ask, double bid)
		{
			try
			{
				if (showTapeStrip)
				{
					if (vol >= tapeStripFilter)
					{
						if (prc >= ask)
						{
							if (TapeStripItems.Count == 0)
							{
								TapeStripItems.Insert(0, new TapeStripItem(1, ask, vol));
							}
							else
							{
								if (TapeStripItems[0].dir != 1)
								{
									TapeStripItems.Insert(0, new TapeStripItem(1, ask, vol));
								}
								else
								{
									if (TapeStripItems[0].prc != ask)
									{
										TapeStripItems.Insert(0, new TapeStripItem(1, ask, vol));
									}
									else
									{
										TapeStripItems[0].vol += vol;
									}
								}
							}
						}
						else if (prc <= bid)
						{
							if (TapeStripItems.Count == 0)
							{
								TapeStripItems.Insert(0, new TapeStripItem(-1, bid, vol));
							}
							else
							{
								if (TapeStripItems[0].dir != -1)
								{
									TapeStripItems.Insert(0, new TapeStripItem(-1, bid, vol));
								}
								else
								{
									if (TapeStripItems[0].prc != bid)
									{
										TapeStripItems.Insert(0, new TapeStripItem(-1, bid, vol));
									}
									else
									{
										TapeStripItems[0].vol += vol;
									}
								}
							}
						}
					}

					if (TapeStripItems.Count > tapeStripMaxItems)
					{
						TapeStripItems.RemoveRange(tapeStripMaxItems, TapeStripItems.Count - tapeStripMaxItems);
					}
				}
			}
			catch (Exception exception)
			{
				if (log)
				{
					Print(exception.ToString());
				}
			}
		}

		#endregion

		#region OnMarketData

		/// OnMarketData
		///
		protected override void OnMarketData(MarketDataEventArgs marketDataUpdate)
		{
			if (!showTapeStrip) { return; }
			if (State != State.Realtime) { return; }

			try
			{
				if (marketDataUpdate.MarketDataType == MarketDataType.Last)
				{
					double prc = marketDataUpdate.Price;
					double ask = marketDataUpdate.Ask;
					double bid = marketDataUpdate.Bid;
					double vol = marketDataUpdate.Volume;

					if (prc >= ask)
					{
						tapeStrip(prc, vol, ask, bid);
					}
					else if (prc <= bid)
					{
						tapeStrip(prc, vol, ask, bid);
					}
				}
			}
			catch (Exception exception)
			{
				if (log)
				{
					Print(exception.ToString());
				}
			}
		}

		#endregion

		#region OnMarketDepth

		protected override void OnMarketDepth(MarketDepthEventArgs e)
		{
			try
			{
				if (BarsInProgress != 0 || !showDepth)
				{
					return;
				}

				lock (e.Instrument.SyncMarketDepth)
				{
					List<DepthRow> rows = (e.MarketDataType == MarketDataType.Ask ? AskDepthRows : BidDepthRows);
					DepthRow row = new DepthRow { prc = e.Price, vol = e.Volume };

					if (e.Operation == Operation.Add || (e.Operation == Operation.Update && (rows.Count == 0 || rows.Count <= e.Position)))
					{
						if (rows.Count <= e.Position)
						{
							rows.Add(row);
						}
						else
						{
							rows.Insert(e.Position, row);
						}
					}
					else if (e.Operation == Operation.Remove && rows.Count >= e.Position)
					{
						rows.RemoveAt(e.Position);
					}
					else if (e.Operation == Operation.Update)
					{
						if (rows[e.Position] == null)
						{
							rows[e.Position] = row;
						}
						else
						{
							rows[e.Position].prc = e.Price;
							rows[e.Position].vol = e.Volume;
						}
					}
				}
			}
			catch (Exception exception)
			{
				if (log)
				{
					NinjaTrader.Code.Output.Process(exception.ToString(), PrintTo.OutputTab1);
				}
			}
		}

		#endregion

		#region OnBarUpdate

		/// OnBarUpdate
		///
		protected override void OnBarUpdate()
		{
			try
			{
				if (CurrentBar < 1) { return; }

				if (Calculate != Calculate.OnEachTick)
				{
					Draw.TextFixed(this, "calculateMessage", "Please set Calculate to 'On each tick' ...", TextPosition.Center);
					return;
				}

				barData.Update();

				#region paint bars

				if (paintBars)
				{
					if (ChartBars != null)
					{
						if (Close[0] > Open[0])
						{
							CandleOutlineBrush = bullishColor;
							BarBrush = bullishColor;
						}
						else if (Close[0] < Open[0])
						{
							CandleOutlineBrush = bearishColor;
							BarBrush = bearishColor;
						}
						else
						{
							CandleOutlineBrush = neutralColor;
							BarBrush = neutralColor;
						}
						if (paintBarsByDelta)
						{
							if (barData.BarItems.IsValidDataPoint(0))
							{
								if (barData.BarItems[0].dtc > 0)
								{
									CandleOutlineBrush = bullishColor;
								}
								else if (barData.BarItems[0].dtc < 0)
								{
									CandleOutlineBrush = bearishColor;
								}
								else
								{
									CandleOutlineBrush = neutralColor;
								}
							}
						}
					}
				}

				#endregion

				#region zig zag

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

				#endregion

				#region midas

				if (showMidas)
				{
					if (CurrentBar >= 2)
					{
						for (int i = MidasItems.Count - 1; i >= 0; i--)
						{
							if (!ZigZagDots.IsValidDataPointAt(MidasItems[i].bar))
							{
								MidasItems.RemoveAt(i);
							}

							if (i < MidasItems.Count - 3)
							{
								break;
							}
						}

						int b = CurrentBar - 2;
						int d = ZigZagDots.GetValueAt(b) == BarsArray[0].GetHigh(b) ? 1 : -1;
						double p = d > 0 ? BarsArray[0].GetHigh(b) : BarsArray[0].GetLow(b);

						if (ZigZagDots.IsValidDataPointAt(b))
						{
							bool midasItemExists = MidasItems.Exists(x => x.bar == b);

							if (!midasItemExists)
							{
								MidasItems.Add(new MidasItem(d, b, p));
							}
						}

						if (State == State.Realtime)
						{
							for (int i = 0; i < MidasItems.Count; i++)
							{
								if (MidasItems[i].run)
								{
									updateMidas(i);
								}
							}
						}
					}
				}

				#endregion

				#region stacked imabalances

				// stacked imbalances

				try
				{
					if (IsFirstTickOfBar)
					{
						if (showStackedImbalances)
						{
							if (barData.BarItems[1] != null)
							{
								// update existing stacked imbalances

								for (int i = 0; i < StackedImbalanceItems.Count; i++)
								{
									if (StackedImbalanceItems[i].dir > 0 && StackedImbalanceItems[i].end == 0)
									{
										if (Low[1] < StackedImbalanceItems[i].min)
										{
											StackedImbalanceItems[i].end = CurrentBar - 1;
										}
									}
									if (StackedImbalanceItems[i].dir < 0 && StackedImbalanceItems[i].end == 0)
									{
										if (High[1] > StackedImbalanceItems[i].max)
										{
											StackedImbalanceItems[i].end = CurrentBar - 1;
										}
									}
								}

								// stacked ask imbalances

								double rowPrc = barData.BarItems[1].max;
								double maxSai = 0.0;
								double minSai = 0.0;
								double maxSbi = 0.0;
								double minSbi = 0.0;

								while (rowPrc >= barData.BarItems[1].min)
								{
									// stacked ask imbalance

									if (barData.BarItems[1].isAskImbalance(rowPrc, rowPrc - TickSize, minImbalanceRatio))
									{
										maxSai = (maxSai == 0.0) ? rowPrc : maxSai;
										minSai = rowPrc;
									}
									else
									{
										if (maxSai > minSai + TickSize)
										{
											if (Low[0] > minSai)
											{
												StackedImbalanceItems.Add(new StackedImbalanceItem(1, CurrentBar - 1, maxSai, minSai));

												if (bullishStackedImbalanceAlert)
												{
													Alert("bullish_stacked_imbalance", Priority.High, "Bullish Stacked Imbalance detected.", bullishStackedImbalanceSound, 0, Brushes.Black, bullishColor);
												}
											}
										}

										maxSai = 0.0;
										minSai = 0.0;
									}

									// stacked bid imbalance

									if (barData.BarItems[1].isBidImbalance(rowPrc + TickSize, rowPrc, minImbalanceRatio))
									{
										maxSbi = (maxSbi == 0.0) ? rowPrc : maxSbi;
										minSbi = rowPrc;
									}
									else
									{
										if (maxSbi > minSbi + TickSize)
										{
											if (High[0] < maxSbi)
											{
												StackedImbalanceItems.Add(new StackedImbalanceItem(-1, CurrentBar - 1, maxSbi, minSbi));

												if (bearishStackedImbalanceAlert)
												{
													Alert("bearish_stacked_imbalance", Priority.High, "Bearish Stacked Imbalance detected.", bearishStackedImbalanceSound, 0, Brushes.Black, bearishColor);
												}
											}
										}

										maxSbi = 0.0;
										minSbi = 0.0;
									}

									rowPrc -= TickSize;
								}

								// stacked ask imbalance

								if (maxSai > minSai + TickSize)
								{
									if (Low[0] > minSai)
									{
										StackedImbalanceItems.Add(new StackedImbalanceItem(1, CurrentBar - 1, maxSai, minSai));

										if (bullishStackedImbalanceAlert)
										{
											Alert("bullish_stacked_imbalance", Priority.High, "Bullish Stacked Imbalance detected.", bullishStackedImbalanceSound, 0, Brushes.Transparent, bullishColor);
										}
									}
								}

								// stacked bid imbalance

								if (maxSbi > minSbi + TickSize)
								{
									if (High[0] < maxSbi)
									{
										StackedImbalanceItems.Add(new StackedImbalanceItem(-1, CurrentBar - 1, maxSbi, minSbi));

										if (bearishStackedImbalanceAlert)
										{
											Alert("bearish_stacked_imbalance", Priority.High, "Bearish Stacked Imbalance detected.", bearishStackedImbalanceSound, 0, Brushes.Transparent, bearishColor);
										}
									}
								}
							}
						}
					}
				}
				catch (Exception exception)
				{
					if (log)
					{
						Print("stacked imbalances - " + exception.ToString());
					}
				}

				patternDetector.OnBarUpdate(CurrentBar);

				#endregion
			}
			catch (Exception exception)
			{
				if (log)
				{
					NinjaTrader.Code.Output.Process(exception.ToString(), PrintTo.OutputTab1);
				}
			}
		}

		#endregion

		#region updateMidas

		// updateMidas
		//
		private void updateMidas(int swingIdx)
		{
			try
			{
				double cumVol = 0.0;
				double cumPV = 0.0;
				double startCumVol = 0.0;
				double startCumPV = 0.0;
				double prevValue = 0.0;
				double currValue = 0.0;
				double denom = 0.0;
				double prc = 0.0;
				double vol = 0.0;

				// ---

				MidasItems[swingIdx].run = true;
				MidasItems[swingIdx].lst = 0;

				MidasItems[swingIdx].vwap.Clear();

				// ---

				for (int b = MidasItems[swingIdx].bar; b <= CurrentBar; b++)
				{
					prc = (MidasItems[swingIdx].dir > 0) ? BarsArray[0].GetHigh(b) : BarsArray[0].GetLow(b);
					vol = BarsArray[0].GetVolume(b);

					cumVol += vol;
					cumPV += prc * vol;

					if (b < MidasItems[swingIdx].bar)
					{
						startCumVol = cumVol;
						startCumPV = cumPV;
					}

					denom = cumVol - startCumVol;

					if (denom == 0)
					{
						denom = 1;
					}

					currValue = (cumPV - startCumPV) / denom;

					if (MidasItems[swingIdx].dir > 0)
					{
						if (prevValue != 0.0)
						{
							if (BarsArray[0].GetClose(b - 1) > Instrument.MasterInstrument.RoundToTickSize(prevValue))
							{
								MidasItems[swingIdx].run = false;
								MidasItems[swingIdx].lst = b;
								break;
							}
						}

						MidasItems[swingIdx].vwap.Add(currValue);
					}

					if (MidasItems[swingIdx].dir < 0)
					{
						if (prevValue != 0.0)
						{
							if (BarsArray[0].GetClose(b - 1) < Instrument.MasterInstrument.RoundToTickSize(prevValue))
							{
								MidasItems[swingIdx].run = false;
								MidasItems[swingIdx].lst = b;
								break;
							}
						}

						MidasItems[swingIdx].vwap.Add(currValue);
					}

					prevValue = currValue;
				}
			}
			catch (Exception exception)
			{
				if (log)
				{
					NinjaTrader.Code.Output.Process(exception.ToString(), PrintTo.OutputTab1);
				}
			}
		}

		#endregion

		#region Text Utilities

		/// getTextSze
		///
		private double getTextSize(ChartScale chartScale, int cellWidth)
		{
			if (footprintDisplayType == FPV2FootprintDisplayType.Profile)
			{
				return ChartControl.Properties.LabelFont.Size;
			}

			if (cellWidth < 14)
			{
				return 0;
			}

			// check height

			float y1 = 0f;
			float y2 = 0f;
			float ls = (float)ChartControl.Properties.LabelFont.Size * 2;
			float ts = float.MaxValue;
			double tp = Instrument.MasterInstrument.RoundToTickSize(chartScale.GetValueByY(ChartPanel.Y));
			double bt = Instrument.MasterInstrument.RoundToTickSize(chartScale.GetValueByY(ChartPanel.H));

			for (double i = bt - TickSize; i <= tp; i += TickSize)
			{
				y1 = ((chartScale.GetYByValue(i) + chartScale.GetYByValue(i + TickSize)) / 2);
				y2 = ((chartScale.GetYByValue(i) + chartScale.GetYByValue(i - TickSize)) / 2);
				ts = (Math.Abs(y1 - y2) < ts) ? Math.Abs(y1 - y2) : ts;
			}

			// check width

			NinjaTrader.NinjaScript.Indicators.Infinity.BarData.BarItem currBarItem;

			double fontSize = 0;
			double maxValue = 0.0;
			float maxWidth = float.MaxValue;

			for (int i = ChartBars.ToIndex; i >= ChartBars.FromIndex; i--)
			{
				if (barData.BarItems.IsValidDataPointAt(i))
				{
					currBarItem = barData.BarItems.GetValueAt(i);
				}
				else
				{
					continue;
				}

				if (currBarItem == null) { continue; }
				if (currBarItem.rowItems.IsEmpty) { continue; }

				foreach (KeyValuePair<double, NinjaTrader.NinjaScript.Indicators.Infinity.BarData.RowItem> ri in currBarItem.rowItems)
				{
					maxValue = (ri.Value.ask > maxValue) ? ri.Value.ask : maxValue;
					maxValue = (ri.Value.bid > maxValue) ? ri.Value.bid : maxValue;
				}
			}

			fontSize = (double)Math.Min(Math.Floor(ts * 0.7), ls);

			if (maxValue > 0.0)
			{
				while (maxWidth > (float)(cellWidth - 10f))
				{
					SimpleFont sf = new SimpleFont("Consolas", fontSize) { Bold = true };
					TextFormat tf = sf.ToDirectWriteTextFormat();
					TextLayout tl = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, maxValue.ToString(), tf, ChartPanel.W, ChartPanel.H);

					maxWidth = tl.Metrics.Width;

					sf = null;
					tf.Dispose();
					tl.Dispose();

					fontSize = fontSize - 1;
				}

			}

			return Math.Max(fontSize, 1);
		}

		/// getCellWidth
		///
		private int getCellWidth()
		{
			if (footprintDisplayType == FPV2FootprintDisplayType.Profile)
			{
				return (int)Math.Round((ChartControl.Properties.BarDistance - (barHalfWidth * 2) - 5) / 2);
			}

			if (footprintDisplayType == FPV2FootprintDisplayType.Numbers)
			{
				if (footprintDeltaProfile)
				{
					return (int)Math.Round((ChartControl.Properties.BarDistance - (barHalfWidth * 2) - 7) / 3);
				}
				else
				{
					return (int)Math.Round((ChartControl.Properties.BarDistance - (barHalfWidth * 2) - 5) / 2);
				}
			}

			return 0;
		}

		#endregion

		#region Color Utilities

		/// invertColor
		///
		private SolidColorBrush invertColor(SolidColorBrush brush)
		{
			try
			{
				byte r = (byte)(255 - ((SolidColorBrush)brush).Color.R);
				byte g = (byte)(255 - ((SolidColorBrush)brush).Color.G);
				byte b = (byte)(255 - ((SolidColorBrush)brush).Color.B);

				return new SolidColorBrush(Color.FromRgb(r, g, b));
			}
			catch (Exception e)
			{
				if (log)
				{
					Print(e.ToString());
				}
			}

			return Brushes.Yellow;
		}

		/// blendColor
		///
		private SolidColorBrush blendColor(SolidColorBrush foreBrush, SolidColorBrush backBrush, double amount)
		{
			try
			{
				byte r = (byte)((((SolidColorBrush)foreBrush).Color.R * amount) + ((SolidColorBrush)backBrush).Color.R * (1.0 - amount));
				byte g = (byte)((((SolidColorBrush)foreBrush).Color.G * amount) + ((SolidColorBrush)backBrush).Color.G * (1.0 - amount));
				byte b = (byte)((((SolidColorBrush)foreBrush).Color.B * amount) + ((SolidColorBrush)backBrush).Color.B * (1.0 - amount));

				return new SolidColorBrush(Color.FromRgb(r, g, b));
			}
			catch (Exception e)
			{
				if (log)
				{
					Print(e.ToString());
				}
			}

			return Brushes.Yellow;
		}

		#endregion

		#region drawLabel

		/// drawLabel
		///
		private float drawLabel(string text, bool bold, float posX, float posY, SharpDX.Direct2D1.Brush labelBrush, float labelBrushOpacity, SharpDX.Direct2D1.Brush textBrush, float textBrushOpacity, string arrows = "left")
		{
			float maxPosX = posX;

			try
			{
				SharpDX.Direct2D1.AntialiasMode oldAntialiasMode = RenderTarget.AntialiasMode;
				RenderTarget.AntialiasMode = SharpDX.Direct2D1.AntialiasMode.Aliased;

				/// ---

				SimpleFont sf = new SimpleFont("Consolas", Math.Ceiling(ChartControl.Properties.LabelFont.Size)) { Bold = bold };
				TextFormat tf = sf.ToDirectWriteTextFormat();
				TextLayout tl = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, text, tf, ChartPanel.W, ChartPanel.H);

				float txtW = tl.Metrics.Width;
				float txtH = tl.Metrics.Height;
				float labW = txtW + 10f;
				float labH = txtH + 4f;

				/// ---

				SharpDX.Direct2D1.PathGeometry geoPath;
				SharpDX.Direct2D1.GeometrySink geoSink;

				List<SharpDX.Vector2> vectors = new List<SharpDX.Vector2>();

				SharpDX.Vector2 labelVec1 = new SharpDX.Vector2(posX, posY);
				SharpDX.Vector2 labelVec2 = new SharpDX.Vector2(labelVec1.X + (float)(labH / 5), (float)(labelVec1.Y - (labH / 2)));
				SharpDX.Vector2 labelVec3 = new SharpDX.Vector2(labelVec2.X + labW, labelVec2.Y);
				SharpDX.Vector2 labelVec4 = new SharpDX.Vector2(labelVec3.X, labelVec3.Y + labH);
				SharpDX.Vector2 labelVec5 = new SharpDX.Vector2(labelVec1.X + (float)(labH / 5), (float)(labelVec1.Y + (labH / 2)));
				SharpDX.Vector2 labelVec6 = new SharpDX.Vector2(posX, posY);

				maxPosX = labelVec3.X;

				SharpDX.RectangleF rect = new SharpDX.RectangleF();

				rect.X = labelVec2.X;
				rect.Y = labelVec2.Y;
				rect.Width = labW;
				rect.Height = labH;

				vectors.Add(labelVec1);
				vectors.Add(labelVec2);
				vectors.Add(labelVec3);
				vectors.Add(labelVec4);
				vectors.Add(labelVec5);
				vectors.Add(labelVec6);

				geoPath = new SharpDX.Direct2D1.PathGeometry(Core.Globals.D2DFactory);
				geoSink = geoPath.Open();

				geoSink.BeginFigure(vectors[0], SharpDX.Direct2D1.FigureBegin.Filled);
				geoSink.AddLines(vectors.ToArray());
				geoSink.EndFigure(SharpDX.Direct2D1.FigureEnd.Open);
				geoSink.Close();

				SharpDX.Direct2D1.Brush labelBrushDX = Brushes.White.ToDxBrush(RenderTarget);

				bckBrush.Opacity = 1f;
				RenderTarget.FillGeometry(geoPath, bckBrush);

				labelBrush.Opacity = labelBrushOpacity;
				RenderTarget.FillGeometry(geoPath, labelBrush);

				/// ---

				tf.TextAlignment = SharpDX.DirectWrite.TextAlignment.Center;

				tl = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, text, tf, rect.Width, rect.Height);

				tl.ParagraphAlignment = SharpDX.DirectWrite.ParagraphAlignment.Center;

				textBrush.Opacity = textBrushOpacity;
				RenderTarget.DrawTextLayout(new SharpDX.Vector2(rect.X, rect.Y), tl, textBrush);

				/// ---

				sf = null;
				tf.Dispose();
				tl.Dispose();

				geoPath.Dispose();
				geoSink.Dispose();

				RenderTarget.AntialiasMode = oldAntialiasMode;
			}
			catch (Exception exception)
			{
				if (log)
				{
					NinjaTrader.Code.Output.Process(exception.ToString(), PrintTo.OutputTab1);
				}
			}

			return maxPosX;
		}

		#endregion

		#region OnRenderTargetChanged

		public override void OnRenderTargetChanged()
		{
			if (brushesInitialized)
			{
				bckBrush.Dispose();
				mapBrush.Dispose();
				pocBrush.Dispose();
				proBrush.Dispose();
				ntlBrush.Dispose();
				askBrush.Dispose();
				bidBrush.Dispose();
				stkBrush.Dispose();
				dupBrush.Dispose();
				ddnBrush.Dispose();

				if (RenderTarget != null)
				{
					bckBrush = bckColor.ToDxBrush(RenderTarget);
					mapBrush = mapColor.ToDxBrush(RenderTarget);
					pocBrush = pocColor.ToDxBrush(RenderTarget);
					proBrush = profileColor.ToDxBrush(RenderTarget);
					ntlBrush = neutralColor.ToDxBrush(RenderTarget);
					askBrush = bullishColor.ToDxBrush(RenderTarget);
					bidBrush = bearishColor.ToDxBrush(RenderTarget);
					stkBrush = stackedColor.ToDxBrush(RenderTarget);
					dupBrush = depthAskColor.ToDxBrush(RenderTarget);
					ddnBrush = depthBidColor.ToDxBrush(RenderTarget);
				}
			}
		}

		#endregion

		#region OnRender

		/// OnRender
		///
		protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			if (ChartBars == null || Bars.Instrument == null || ChartControl == null || IsInHitTest) { return; }

			base.OnRender(chartControl, chartScale);

			/// ---

			if (forceRefresh)
			{
				forceRefresh = false;

				refreshChart();
			}

			/// ---

			try
			{
				cScale = chartScale;
				scrollFixedScale(chartScale);

				/// ---

				barFullWidth = chartControl.GetBarPaintWidth(ChartBars);
				barHalfWidth = ((barFullWidth - 1) / 2) + 2;

				cellWidth = (showFootprint) ? getCellWidth() : 0;
				dTextSize = (showFootprint) ? getTextSize(chartScale, cellWidth) : ChartControl.Properties.LabelFont.Size;

				/// ---

				int bmr = rightMargin;

				bmr += (showDepth && depthWidth > 0) ? depthWidth + 30 : 0;

				bmr += (prevProfileShow && prevProfileWidth > 0) ? prevProfileWidth : 0;
				bmr += (prevProfileShow && prevProfileBar) ? 6 : 0;
				bmr += (prevProfileShow && (prevProfileWidth > 0 || prevProfileBar)) ? 30 : 0;

				bmr += (currProfileShow && currProfileWidth > 0) ? currProfileWidth : 0;
				bmr += (currProfileShow && currProfileBar) ? 6 : 0;
				bmr += (currProfileShow && (currProfileWidth > 0 || currProfileBar)) ? 30 : 0;

				bmr += (custProfileShow && custProfileWidth > 0) ? custProfileWidth + 42 : 0;

				bmr += (showFootprint && footprintDisplayType == FPV2FootprintDisplayType.Numbers && footprintDeltaProfile) ? (cellWidth + 2) : 0;
				bmr += (bmr > 0) ? 10 : 0;

				bmr += cellWidth;

				bmr += leftMargin;

				if (ChartControl.Properties.BarMarginRight != bmr)
				{
					ChartControl.Properties.BarMarginRight = bmr;
				}

				/// ---

				if (!brushesInitialized)
				{
					bckColor = (SolidColorBrush)ChartControl.Properties.ChartBackground.Clone();
					bckBrush = bckColor.ToDxBrush(RenderTarget);
					mapBrush = mapColor.ToDxBrush(RenderTarget);
					pocBrush = pocColor.ToDxBrush(RenderTarget);
					proBrush = profileColor.ToDxBrush(RenderTarget);
					ntlBrush = neutralColor.ToDxBrush(RenderTarget);
					askBrush = bullishColor.ToDxBrush(RenderTarget);
					bidBrush = bearishColor.ToDxBrush(RenderTarget);
					stkBrush = stackedColor.ToDxBrush(RenderTarget);
					dupBrush = depthAskColor.ToDxBrush(RenderTarget);
					ddnBrush = depthBidColor.ToDxBrush(RenderTarget);

					brushesInitialized = true;
				}

				/// ---

				drawMap(chartControl, chartScale);
				drawStackedImbalances(chartControl, chartScale);
				drawDepth(chartControl, chartScale);
				drawMidas(chartControl, chartScale);
				drawProfiles(chartControl, chartScale);
				drawClose(chartControl, chartScale);
				drawFootPrint(chartControl, chartScale);
				drawFootPrintBarInfo(chartControl, chartScale);
				drawPoc(chartControl, chartScale);
				drawTapeStrip(chartControl, chartScale);
				drawBottomArea(chartControl, chartScale);
				drawPatterns(chartControl, chartScale);
			}
			catch (Exception exception)
			{
				if (log)
				{
					NinjaTrader.Code.Output.Process(exception.ToString(), PrintTo.OutputTab1);
				}
			}
		}

		#endregion

		#region scrollFixedScale

		/// scrollFixedScale
		///
		private void scrollFixedScale(ChartScale chartScale)
		{
			try
			{
				if (chartScale.Properties.YAxisRangeType == YAxisRangeType.Fixed)
				{
					double prcRng = 0.0;
					double upperM = 0.0;
					double lowerM = 0.0;
					double currCl = BarsArray[0].GetClose(ChartBars.ToIndex);
					double currHi = BarsArray[0].GetHigh(ChartBars.ToIndex);
					double currLo = BarsArray[0].GetLow(ChartBars.ToIndex);
					double prcDif = 0.0;

					currHi += (showFootprint) ? TickSize / 2.0 : 0;

					prcRng = chartScale.MaxMinusMin;
					upperM = (prcRng / 100.0) * chartScale.Properties.AutoScaleMarginUpper;
					lowerM = (prcRng / 100.0) * chartScale.Properties.AutoScaleMarginLower;

					if (currCl > chartScale.MaxValue - upperM)
					{
						prcDif = currHi - (chartScale.MaxValue - upperM);
						chartScale.Properties.FixedScaleMax = chartScale.Properties.FixedScaleMax + prcDif;
						chartScale.Properties.FixedScaleMin = chartScale.Properties.FixedScaleMin + prcDif;

						refreshChart();
					}
					else if (currCl < chartScale.MinValue + lowerM)
					{
						prcDif = chartScale.MinValue + lowerM - currLo;
						chartScale.Properties.FixedScaleMax = chartScale.Properties.FixedScaleMax - prcDif;
						chartScale.Properties.FixedScaleMin = chartScale.Properties.FixedScaleMin - prcDif;

						refreshChart();
					}
				}
			}
			catch (Exception exception)
			{
				if (log)
				{
					NinjaTrader.Code.Output.Process(exception.ToString(), PrintTo.OutputTab1);
				}
			}
		}

		#endregion

		#region drawClose

		/// drawClose
		///
		private void drawClose(ChartControl chartControl, ChartScale chartScale)
		{
			try
			{
				if (!showClose) { return; }

				SharpDX.Direct2D1.AntialiasMode oldAntialiasMode = RenderTarget.AntialiasMode;
				RenderTarget.AntialiasMode = SharpDX.Direct2D1.AntialiasMode.Aliased;

				SharpDX.RectangleF rect = new SharpDX.RectangleF();
				SharpDX.Vector2 vec1 = new SharpDX.Vector2();
				SharpDX.Vector2 vec2 = new SharpDX.Vector2();

				double ask = GetCurrentAsk();
				double bid = GetCurrentBid();
				double cls = BarsArray[0].GetClose(ChartBars.ToIndex);

				float x1, x2, y1, y2, wd, ht = 0f;

				/// ---

				x1 = chartControl.CanvasLeft;
				x2 = chartControl.CanvasRight;
				wd = Math.Abs(x1 - x2);

				/// area

				y1 = chartScale.GetYByValue(GetCurrentAsk());//((chartScale.GetYByValue(cls) + chartScale.GetYByValue(cls + TickSize)) / 2);
				y2 = chartScale.GetYByValue(GetCurrentBid());//((chartScale.GetYByValue(cls) + chartScale.GetYByValue(cls - TickSize)) / 2);
				ht = Math.Abs(y2 - y1) - 1f;

				rect.X = (float)x1;
				rect.Y = (float)y1;
				rect.Width = (float)wd;
				rect.Height = (float)ht;

				ntlBrush.Opacity = 0.1f;

				RenderTarget.FillRectangle(rect, ntlBrush);

				/// line

				y1 = chartScale.GetYByValue(cls);
				y2 = y1;

				vec1.X = x1;
				vec1.Y = y1;

				vec2.X = x2;
				vec2.Y = y2;

				if (cls >= ask)
				{
					askBrush.Opacity = 0.5f;

					RenderTarget.DrawLine(vec1, vec2, askBrush, 1, dLine.StrokeStyle);
				}
				else if (cls <= bid)
				{
					bidBrush.Opacity = 0.5f;

					RenderTarget.DrawLine(vec1, vec2, bidBrush, 1, dLine.StrokeStyle);
				}
				else
				{
					ntlBrush.Opacity = 0.5f;

					RenderTarget.DrawLine(vec1, vec2, ntlBrush, 1, dLine.StrokeStyle);
				}

				/// ---

				RenderTarget.AntialiasMode = oldAntialiasMode;
			}
			catch (Exception exception)
			{
				if (log)
				{
					NinjaTrader.Code.Output.Process(exception.ToString(), PrintTo.OutputTab1);
				}
			}
		}

		#endregion

		#region drawMap

		/// drawMap
		///
		private void drawMap(ChartControl chartControl, ChartScale chartScale)
		{
			try
			{
				if (!custProfileShow || !custProfileMap) { return; }

				SharpDX.Direct2D1.AntialiasMode oldAntialiasMode = RenderTarget.AntialiasMode;
				RenderTarget.AntialiasMode = SharpDX.Direct2D1.AntialiasMode.Aliased;

				SharpDX.RectangleF rect = new SharpDX.RectangleF();

				float rx, x1, x2, y1, y2, wd, ht = 0f;

				float barWidth = (float)chartControl.GetBarPaintWidth(chartControl.BarsArray[0]);
				barWidth = (showFootprint) ? (float)(barWidth + cellWidth * 2 + 4f) : barWidth;

				NinjaTrader.NinjaScript.Indicators.Infinity.BarData.BarItem barItem;

				double sRng = 0.0;
				float oRng = (custProfileMapType == FPV2MapDisplayType.Volume) ? mapMaxOpa : mapMaxOpa / 2;
				double size = 0.0;
				float opac = 0.0f;
				double cont = 0.0;

				/// ---

				for (int i = ChartBars.ToIndex; i >= Math.Max(ChartBars.FromIndex - 1, 0); i--)
				{
					if (barData.BarItems.IsValidDataPointAt(i))
					{
						barItem = barData.BarItems.GetValueAt(i);

						if (barItem == null) { continue; }
						if (barItem.custProfile.rowItems.IsEmpty) { continue; }

						x1 = chartControl.GetXByBarIndex(ChartBars, i);
						x2 = chartControl.GetXByBarIndex(ChartBars, i + 1);

						if (i == ChartBars.ToIndex)
						{
							rx = chartControl.CanvasRight - rightMargin;

							rx -= (showDepth && depthWidth > 0) ? depthWidth + 30 : 0;

							rx -= (prevProfileShow && prevProfileWidth > 0) ? prevProfileWidth : 0;
							rx -= (prevProfileShow && prevProfileBar) ? 6 : 0;
							rx -= (prevProfileShow && (prevProfileWidth > 0 || prevProfileBar)) ? 30 : 0;

							rx -= (currProfileShow && currProfileWidth > 0) ? currProfileWidth : 0;
							rx -= (currProfileShow && currProfileBar) ? 6 : 0;
							rx -= (currProfileShow && (currProfileWidth > 0 || currProfileBar)) ? 30 : 0;

							rx -= (custProfileShow && custProfileWidth > 0) ? 9f : 0f;

							x2 = rx;
						}

						wd = Math.Abs(x1 - x2);

						sRng = (custProfileMapType == FPV2MapDisplayType.Volume) ? barItem.custProfile.rowItems[barItem.custProfile.poc].vol : barItem.custProfile.getMaxDta();
						size = 0.0;
						opac = 0.0f;
						cont = 0.0;

						foreach (KeyValuePair<double, NinjaTrader.NinjaScript.Indicators.Infinity.BarData.RowItem> ri in barItem.custProfile.rowItems)
						{
							size = (custProfileMapType == FPV2MapDisplayType.Volume) ? ri.Value.vol : Math.Abs(ri.Value.dta);
							opac = (float)Math.Round((oRng / sRng) * size, 5);

							opac = (float)((opac - 0f) * (oRng / (oRng - 0f)));

							cont = (oRng - opac) * 2.0;
							opac = (float)((opac - 0f) * (oRng / ((oRng + cont) - 0f)));

							opac = (custProfileMapType == FPV2MapDisplayType.Volume && ri.Key == barItem.custProfile.poc) ? opac + 0.3f : opac;
							opac = Math.Min(1.0f, opac);

							if (opac > 0.01f)
							{
								y1 = ((chartScale.GetYByValue(ri.Key) + chartScale.GetYByValue(ri.Key + TickSize)) / 2);
								y2 = ((chartScale.GetYByValue(ri.Key) + chartScale.GetYByValue(ri.Key - TickSize)) / 2);

								ht = Math.Abs(y2 - y1);

								rect.X = (float)x1;
								rect.Y = (float)y1;
								rect.Width = (float)wd;
								rect.Height = (float)ht;

								if (custProfileMapType == FPV2MapDisplayType.Volume)
								{
									mapBrush.Opacity = opac;
									RenderTarget.FillRectangle(rect, mapBrush);
								}
								else
								{
									if (ri.Value.dta > 0.0)
									{
										askBrush.Opacity = opac;
										RenderTarget.FillRectangle(rect, askBrush);
									}
									if (ri.Value.dta < 0.0)
									{
										bidBrush.Opacity = opac;
										RenderTarget.FillRectangle(rect, bidBrush);
									}
								}
							}
						}
					}
				}

				/// ---

				RenderTarget.AntialiasMode = oldAntialiasMode;
			}
			catch (Exception exception)
			{
				if (log)
				{
					NinjaTrader.Code.Output.Process(exception.ToString(), PrintTo.OutputTab1);
				}
			}
		}

		#endregion

		#region drawStackedImbalances

		/// drawStackedImbalances
		///
		private void drawStackedImbalances(ChartControl chartControl, ChartScale chartScale)
		{
			if (!showStackedImbalances) { return; }
			if (StackedImbalanceItems.Count == 0) { return; }

			SharpDX.Direct2D1.AntialiasMode oldAntialiasMode = RenderTarget.AntialiasMode;
			RenderTarget.AntialiasMode = SharpDX.Direct2D1.AntialiasMode.Aliased;

			int x1 = 0;
			int x2 = 0;
			float y1 = 0;
			float y2 = 0;

			SharpDX.RectangleF rect = new SharpDX.RectangleF();

			for (int i = 0; i < StackedImbalanceItems.Count; i++)
			{
				if (StackedImbalanceItems[i].end == 0 || StackedImbalanceItems[i].end > ChartBars.FromIndex)
				{
					x1 = chartControl.GetXByBarIndex(ChartBars, StackedImbalanceItems[i].bar);
					x2 = (StackedImbalanceItems[i].end == 0) ? chartControl.CanvasRight : chartControl.GetXByBarIndex(ChartBars, StackedImbalanceItems[i].end);

					y1 = ((chartScale.GetYByValue(StackedImbalanceItems[i].max) + chartScale.GetYByValue(StackedImbalanceItems[i].max + TickSize)) / 2) + 1;
					y2 = ((chartScale.GetYByValue(StackedImbalanceItems[i].min) + chartScale.GetYByValue(StackedImbalanceItems[i].min - TickSize)) / 2) - 1;

					rect.X = (float)x1;
					rect.Y = (float)y1;
					rect.Width = (float)Math.Abs(x1 - x2);
					rect.Height = (float)Math.Abs(y1 - y2);

					if (StackedImbalanceItems[i].dir > 0)
					{
						if (!custProfileMap)
						{
							stkBrush.Opacity = 0.04f;
							RenderTarget.FillRectangle(rect, stkBrush);
						}

						stkBrush.Opacity = stackedImbOpa;
						RenderTarget.DrawRectangle(rect, stkBrush);
					}
					if (StackedImbalanceItems[i].dir < 0)
					{
						if (!custProfileMap)
						{
							stkBrush.Opacity = 0.04f;
							RenderTarget.FillRectangle(rect, stkBrush);
						}

						stkBrush.Opacity = stackedImbOpa;
						RenderTarget.DrawRectangle(rect, stkBrush);
					}
				}
			}

			/// ---

			RenderTarget.AntialiasMode = oldAntialiasMode;
		}

		#endregion

		#region drawDepth

		/// drawDepth
		///
		private void drawDepth(ChartControl chartControl, ChartScale chartScale)
		{
			try
			{
				if (!showDepth || depthWidth <= 0 || (AskDepthRows.Count == 0 && BidDepthRows.Count == 0))
				{
					return;
				}

				SharpDX.Direct2D1.AntialiasMode oldAntialiasMode = RenderTarget.AntialiasMode;
				RenderTarget.AntialiasMode = SharpDX.Direct2D1.AntialiasMode.Aliased;

				SharpDX.RectangleF rect = new SharpDX.RectangleF();
				SharpDX.Vector2 vec1 = new SharpDX.Vector2();
				SharpDX.Vector2 vec2 = new SharpDX.Vector2();

				float x1, x2, y1, y2, wd, ht = 0f;

				double rngMin = Instrument.MasterInstrument.RoundToTickSize(chartScale.MinValue) - TickSize;
				double rngMax = Instrument.MasterInstrument.RoundToTickSize(chartScale.MaxValue) + TickSize;
				long minVol = long.MaxValue;
				long maxVol = long.MinValue;
				long curVol = 0;
				double curPrc = 0;
				float oRng = depthMaxOpa;
				float opac = oRng;

				float mh = float.MaxValue;
				float rx = (float)(chartControl.CanvasRight - rightMargin);

				for (int i = 0; i < AskDepthRows.Count; i++)
				{
					if (AskDepthRows[i].prc < rngMin || AskDepthRows[i].prc > rngMax) { continue; }

					minVol = (AskDepthRows[i].vol < minVol) ? AskDepthRows[i].vol : minVol;
					maxVol = (AskDepthRows[i].vol > maxVol) ? AskDepthRows[i].vol : maxVol;
				}

				for (int i = 0; i < BidDepthRows.Count; i++)
				{
					if (BidDepthRows[i].prc < rngMin || BidDepthRows[i].prc > rngMax) { continue; }

					minVol = (BidDepthRows[i].vol < minVol) ? BidDepthRows[i].vol : minVol;
					maxVol = (BidDepthRows[i].vol > maxVol) ? BidDepthRows[i].vol : maxVol;
				}

				if (minVol == long.MaxValue || maxVol == long.MinValue)
				{
					return;
				}

				// draw background

				rect.X = (float)(rx - depthWidth);
				rect.Y = 0f;
				rect.Width = (float)depthWidth;
				rect.Height = (float)chartScale.Height;

				RenderTarget.FillRectangle(rect, bckBrush);

				// draw ask profile

				for (int i = 0; i < AskDepthRows.Count; i++)
				{
					if (AskDepthRows[i].prc < rngMin || AskDepthRows[i].prc > rngMax) { continue; }

					curPrc = AskDepthRows[i].prc;
					curVol = AskDepthRows[i].vol;

					y1 = (chartScale.GetYByValue(curPrc) + chartScale.GetYByValue(curPrc + TickSize)) / 2;
					y2 = (chartScale.GetYByValue(curPrc) + chartScale.GetYByValue(curPrc - TickSize)) / 2;

					ht = Math.Abs(y2 - y1);
					mh = (ht < mh) ? ht : mh;

					wd = (float)Math.Round(((float)depthWidth / maxVol) * curVol);
					wd = Math.Max(2f, wd);

					opac = (depthGradient) ? (float)Math.Round((oRng / maxVol) * curVol, 5) + 0.1f : oRng;

					rect.X = (float)(rx - wd);
					rect.Y = (float)y1;
					rect.Width = (float)wd;
					rect.Height = (float)ht;

					if (bor)
					{
						bckBrush.Opacity = 1.0f;

						RenderTarget.DrawRectangle(rect, bckBrush);
						RenderTarget.FillRectangle(rect, bckBrush);

						rect.Width -= 1f;
						rect.Height -= 1f;
					}
					else
					{
						bckBrush.Opacity = 1.0f;

						RenderTarget.FillRectangle(rect, bckBrush);
					}

					dupBrush.Opacity = opac;

					if (opac >= 0.01f)
					{
						RenderTarget.FillRectangle(rect, dupBrush);
					}
				}

				// draw bid profile

				for (int i = 0; i < BidDepthRows.Count; i++)
				{
					if (BidDepthRows[i].prc < rngMin || BidDepthRows[i].prc > rngMax) { continue; }

					curPrc = BidDepthRows[i].prc;
					curVol = BidDepthRows[i].vol;

					y1 = (chartScale.GetYByValue(curPrc) + chartScale.GetYByValue(curPrc + TickSize)) / 2;
					y2 = (chartScale.GetYByValue(curPrc) + chartScale.GetYByValue(curPrc - TickSize)) / 2;

					ht = Math.Abs(y2 - y1);
					mh = (ht < mh) ? ht : mh;

					wd = (float)Math.Round(((float)depthWidth / maxVol) * curVol);
					wd = Math.Max(2f, wd);

					opac = (depthGradient) ? (float)Math.Round((oRng / maxVol) * curVol, 5) + 0.1f : oRng;

					rect.X = (float)(rx - wd);
					rect.Y = (float)y1;
					rect.Width = (float)wd;
					rect.Height = (float)ht;

					if (bor)
					{
						bckBrush.Opacity = 1.0f;

						RenderTarget.DrawRectangle(rect, bckBrush);
						RenderTarget.FillRectangle(rect, bckBrush);

						rect.Width -= 1f;
						rect.Height -= 1f;
					}
					else
					{
						bckBrush.Opacity = 1.0f;

						RenderTarget.FillRectangle(rect, bckBrush);
					}

					ddnBrush.Opacity = opac;

					if (opac >= 0.01f)
					{
						RenderTarget.FillRectangle(rect, ddnBrush);
					}
				}

				RenderTarget.AntialiasMode = oldAntialiasMode;
			}
			catch (Exception exception)
			{
				if (log)
				{
					NinjaTrader.Code.Output.Process(exception.ToString(), PrintTo.OutputTab1);
				}
			}
		}

		#endregion

		#region drawProfiles

		/// drawProfiles
		///
		private void drawProfiles(ChartControl chartControl, ChartScale chartScale)
		{
			if (!barData.BarItems.IsValidDataPointAt(ChartBars.ToIndex)) { return; }

			float rx = chartControl.CanvasRight - rightMargin;

			NinjaTrader.NinjaScript.Indicators.Infinity.BarData.Profile prevProfile = barData.BarItems.GetValueAt(ChartBars.ToIndex).prevProfile;
			NinjaTrader.NinjaScript.Indicators.Infinity.BarData.Profile currProfile = barData.BarItems.GetValueAt(ChartBars.ToIndex).currProfile;
			NinjaTrader.NinjaScript.Indicators.Infinity.BarData.Profile custProfile = barData.BarItems.GetValueAt(ChartBars.ToIndex).custProfile;

			rx -= (showDepth && depthWidth > 0) ? depthWidth + 30 : 0;

			if (prevProfileShow && !prevProfile.rowItems.IsEmpty)
			{
				drawProfile(prevProfile, rx, prevProfileWidth, prevProfileGradient, prevProfileValueArea, prevProfileExtendVa, prevProfileBar, false, prevProfileShowDelta, prevProfileShowInfo, chartControl, chartScale);
			}

			rx -= (prevProfileShow && prevProfileWidth > 0) ? prevProfileWidth : 0;
			rx -= (prevProfileShow && prevProfileBar) ? 6 : 0;
			rx -= (prevProfileShow && (prevProfileWidth > 0 || prevProfileBar)) ? 30 : 0;

			if (currProfileShow && !currProfile.rowItems.IsEmpty)
			{
				drawProfile(currProfile, rx, currProfileWidth, currProfileGradient, currProfileValueArea, currProfileExtendVa, currProfileBar, false, currProfileShowDelta, currProfileShowInfo, chartControl, chartScale);
			}

			rx -= (currProfileShow && currProfileWidth > 0) ? currProfileWidth : 0;
			rx -= (currProfileShow && currProfileBar) ? 6 : 0;
			rx -= (currProfileShow && (currProfileWidth > 0 || currProfileBar)) ? 30 : 0;

			if (custProfileShow && !custProfile.rowItems.IsEmpty)
			{
				drawProfile(custProfile, rx, custProfileWidth, custProfileGradient, custProfileValueArea, custProfileExtendVa, false, true, custProfileShowDelta, custProfileShowInfo, chartControl, chartScale);
			}
		}

		#endregion

		#region drawProfile

		/// drawProfile
		///
		private void drawProfile(NinjaTrader.NinjaScript.Indicators.Infinity.BarData.Profile profile, float rx, float profileWidth, bool gradient, bool valueArea, bool extendValueArea, bool bar, bool imbalances, bool showDelta, bool showInfo, ChartControl chartControl, ChartScale chartScale)
		{
			try
			{
				if (profileWidth == 0 && !bar) { return; }
				if (barData.BarItems.GetValueAt(ChartBars.ToIndex) == null) { return; }

				SharpDX.Direct2D1.AntialiasMode oldAntialiasMode = RenderTarget.AntialiasMode;
				RenderTarget.AntialiasMode = SharpDX.Direct2D1.AntialiasMode.Aliased;

				SharpDX.RectangleF rect = new SharpDX.RectangleF();
				SharpDX.Vector2 vec1 = new SharpDX.Vector2();
				SharpDX.Vector2 vec2 = new SharpDX.Vector2();

				float x1, x2, y1, y2, wd, ht = 0f;

				double rngMin = chartScale.MinValue;
				double rngMax = chartScale.MaxValue;

				float barWidth = (bar) ? 6f : 2f;
				float imbWidth = (imbalances) ? 8f : 0f;

				float mh = float.MaxValue;

				/// ---

				#region Background

				if (
				!imbalances ||
				imbalances && !custProfileMap
				)
				{
					x1 = rx - profileWidth - barWidth - imbWidth;
					x2 = rx - imbWidth - barWidth + 1f;

					wd = Math.Abs(x2 - x1);

					y1 = ((chartScale.GetYByValue(profile.max) + chartScale.GetYByValue(profile.max + TickSize)) / 2);
					y2 = ((chartScale.GetYByValue(profile.min) + chartScale.GetYByValue(profile.min - TickSize)) / 2);

					ht = Math.Abs(y2 - y1);

					rect.X = x1;
					rect.Y = y1;
					rect.Width = wd;
					rect.Height = ht;

					if (profile.dta > 0.0)
					{
						askBrush.Opacity = 0.03f;
						RenderTarget.FillRectangle(rect, askBrush);
					}
					else if (profile.dta < 0.0)
					{
						bidBrush.Opacity = 0.03f;
						RenderTarget.FillRectangle(rect, bidBrush);
					}
					else
					{
						proBrush.Opacity = 0.03f;
						RenderTarget.FillRectangle(rect, proBrush);
					}
				}

				#endregion

				#region Value Area

				if (valueArea)
				{
					if (extendValueArea)
					{
						/// vah

						x1 = rx - barWidth - imbWidth;
						x2 = chartControl.GetXByBarIndex(ChartBars, profile.bar - 1);

						wd = Math.Abs(x2 - x1);

						y1 = ((chartScale.GetYByValue(profile.vah) + chartScale.GetYByValue(profile.vah + TickSize)) / 2);
						y2 = ((chartScale.GetYByValue(profile.vah) + chartScale.GetYByValue(profile.vah - TickSize)) / 2);

						ht = Math.Abs(y2 - y1);
						mh = (ht < mh) ? ht : mh;

						rect.X = x2;
						rect.Y = y1;
						rect.Width = wd;
						rect.Height = ht;

						if (bor)
						{
							rect.Width -= 1f;
							rect.Height -= 1f;
						}

						askBrush.Opacity = 0.2f;

						RenderTarget.FillRectangle(rect, askBrush);

						/// poc

						x1 = rx - barWidth - imbWidth;
						x2 = chartControl.GetXByBarIndex(ChartBars, profile.bar - 1);

						wd = Math.Abs(x2 - x1);

						y1 = ((chartScale.GetYByValue(profile.poc) + chartScale.GetYByValue(profile.poc + TickSize)) / 2);
						y2 = ((chartScale.GetYByValue(profile.poc) + chartScale.GetYByValue(profile.poc - TickSize)) / 2);

						ht = Math.Abs(y2 - y1);
						mh = (ht < mh) ? ht : mh;

						rect.X = x2;
						rect.Y = y1;
						rect.Width = wd;
						rect.Height = ht;

						if (bor)
						{
							rect.Width -= 1f;
							rect.Height -= 1f;
						}

						pocBrush.Opacity = 0.2f;

						RenderTarget.FillRectangle(rect, pocBrush);

						/// val

						x1 = rx - barWidth - imbWidth;
						x2 = x2 = chartControl.GetXByBarIndex(ChartBars, profile.bar - 1);

						wd = Math.Abs(x2 - x1);

						y1 = ((chartScale.GetYByValue(profile.val) + chartScale.GetYByValue(profile.val + TickSize)) / 2);
						y2 = ((chartScale.GetYByValue(profile.val) + chartScale.GetYByValue(profile.val - TickSize)) / 2);

						ht = Math.Abs(y2 - y1);

						rect.X = x2;
						rect.Y = y1;
						rect.Width = wd;
						rect.Height = ht;

						if (bor)
						{
							rect.Width -= 1f;
							rect.Height -= 1f;
						}

						bidBrush.Opacity = 0.2f;

						RenderTarget.FillRectangle(rect, bidBrush);

						/// ---

						bor = (mh >= 3f) ? true : false;
					}
					else
					{
						/// vah

						x1 = rx - barWidth - imbWidth;
						x2 = rx - profileWidth - barWidth - imbWidth;

						y1 = chartScale.GetYByValue(profile.vah);
						y2 = y1;

						vec1.X = x1;
						vec1.Y = y1;

						vec2.X = x2;
						vec2.Y = y2;

						askBrush.Opacity = 0.6f;

						RenderTarget.DrawLine(vec1, vec2, askBrush, 1, dLine.StrokeStyle);

						/// val

						x1 = rx - barWidth - imbWidth;
						x2 = rx - profileWidth - barWidth - imbWidth;

						y1 = chartScale.GetYByValue(profile.val);
						y2 = y1;

						vec1.X = x1;
						vec1.Y = y1;

						vec2.X = x2;
						vec2.Y = y2;

						bidBrush.Opacity = 0.6f;

						RenderTarget.DrawLine(vec1, vec2, bidBrush, 1, dLine.StrokeStyle);
					}
				}

				#endregion

				#region Profile

				/// profile -----------------------------------------------------------------------------

				if (profileWidth > 0f)
				{
					double pocPrc = profile.poc;
					double pocVol = (profile.rowItems.ContainsKey(profile.poc)) ? profile.rowItems[profile.poc].vol : 0.0;

					double vahPrc = profile.vah;
					double vahVol = (profile.rowItems.ContainsKey(profile.vah)) ? profile.rowItems[profile.vah].vol : 0.0;

					double valPrc = profile.val;
					double valVol = (profile.rowItems.ContainsKey(profile.val)) ? profile.rowItems[profile.val].vol : 0.0;

					float oRng = profileMaxOpa;
					float opac = oRng;

					double dtc = 0.0;

					foreach (KeyValuePair<double, NinjaTrader.NinjaScript.Indicators.Infinity.BarData.RowItem> ri in profile.rowItems)
					{
						if (ri.Key < rngMin || ri.Key > rngMax) { continue; }

						y1 = (chartScale.GetYByValue(ri.Key) + chartScale.GetYByValue(ri.Key + TickSize)) / 2;
						y2 = (chartScale.GetYByValue(ri.Key) + chartScale.GetYByValue(ri.Key - TickSize)) / 2;

						ht = Math.Abs(y2 - y1);
						mh = (ht < mh) ? ht : mh;

						wd = (float)Math.Round(((profileWidth) / pocVol) * ri.Value.vol);
						wd = Math.Max(2f, wd);

						opac = (gradient) ? (float)Math.Round((oRng / pocVol) * ri.Value.vol, 5) + 0.1f : oRng;

						rect.X = (float)(rx - barWidth - imbWidth - wd);
						rect.Y = (float)y1;
						rect.Width = (float)wd;
						rect.Height = (float)ht;

						if (bor)
						{
							bckBrush.Opacity = 1.0f;

							RenderTarget.DrawRectangle(rect, bckBrush);
							RenderTarget.FillRectangle(rect, bckBrush);

							rect.Width -= 1f;
							rect.Height -= 1f;
						}
						else
						{
							bckBrush.Opacity = 1.0f;

							RenderTarget.FillRectangle(rect, bckBrush);
						}

						proBrush.Opacity = opac;

						if (opac >= 0.01f)
						{
							if (ri.Key == pocPrc)
							{
								pocBrush.Opacity = opac;

								RenderTarget.FillRectangle(rect, pocBrush);
							}
							else
							{
								RenderTarget.FillRectangle(rect, proBrush);
							}
						}

						/// - value area

						if (valueArea)
						{
							if (ri.Key == profile.vah)
							{
								askBrush.Opacity = 0.3f;

								RenderTarget.FillRectangle(rect, askBrush);
							}

							if (ri.Key == profile.val)
							{
								bidBrush.Opacity = 0.3f;

								RenderTarget.FillRectangle(rect, bidBrush);
							}
						}

						/// - delta

						if (showDelta)
						{
							dtc = profile.getDelta(ri.Key);
							wd = (float)Math.Round((wd / ri.Value.vol) * Math.Abs(dtc));

							if (wd >= 1f)
							{
								rect.X = (float)(rx - barWidth - imbWidth - wd);
								rect.Y = (float)y1;
								rect.Width = (float)wd;
								rect.Height = (float)ht;

								if (bor)
								{
									rect.Width -= 1f;
									rect.Height -= 1f;
								}

								if (dtc > 0.0)
								{
									askBrush.Opacity = 0.3f;

									RenderTarget.FillRectangle(rect, askBrush);
								}

								if (dtc < 0.0)
								{
									bidBrush.Opacity = 0.3f;

									RenderTarget.FillRectangle(rect, bidBrush);
								}
							}
						}

						/// - imbalances

						if (imbalances)
						{
							rect.X = (float)(rx - imbWidth);
							rect.Y = (float)y1;
							rect.Width = 3f;
							rect.Height = (float)ht;

							if (bor)
							{
								rect.Height -= 1f;
							}

							if (profile.isBidImbalance(ri.Key + TickSize, ri.Key, minImbalanceRatio))
							{
								bidBrush.Opacity = 0.7f;

								RenderTarget.FillRectangle(rect, bidBrush);
							}
							else
							{
								proBrush.Opacity = 0.1f;

								RenderTarget.FillRectangle(rect, proBrush);
							}

							rect.X = (float)(rx - imbWidth + 4f);
							rect.Y = (float)y1;
							rect.Width = 3f;
							rect.Height = (float)ht;

							if (bor)
							{
								rect.Height -= 1f;
							}

							if (profile.isAskImbalance(ri.Key, ri.Key - TickSize, minImbalanceRatio))
							{
								askBrush.Opacity = 0.7f;

								RenderTarget.FillRectangle(rect, askBrush);
							}
							else
							{
								proBrush.Opacity = 0.1f;

								RenderTarget.FillRectangle(rect, proBrush);
							}
						}
					}
				}

				#endregion

				#region outline
				/*
				if(bor)
				{
					/// poc
					
					wd = (float)Math.Round(((profileWidth) / pocVol) * pocVol);
					wd = Math.Max(2f, wd);
					
					y1 = ((chartScale.GetYByValue(profile.poc) + chartScale.GetYByValue(profile.poc + TickSize)) / 2);
					y2 = ((chartScale.GetYByValue(profile.poc) + chartScale.GetYByValue(profile.poc - TickSize)) / 2);
					
					ht = Math.Abs(y2 - y1);
					mh = (ht < mh) ? ht : mh;
					
					rect.X      = (float)(rx - barWidth - imbWidth - wd);
					rect.Y      = (float)y1;
					rect.Width  = (float)wd;
					rect.Height = (float)ht;
					
					pocBrush.Opacity = 1f;
					
					SharpDX.Direct2D1.LinearGradientBrush gradBrush = new SharpDX.Direct2D1.LinearGradientBrush(RenderTarget, new SharpDX.Direct2D1.LinearGradientBrushProperties()
					{
						StartPoint = new SharpDX.Vector2(rect.X - wd, 0),
						EndPoint   = new SharpDX.Vector2(rect.X + wd, 0),
					},
					new SharpDX.Direct2D1.GradientStopCollection(RenderTarget, new SharpDX.Direct2D1.GradientStop[]
					{
						new	SharpDX.Direct2D1.GradientStop()
						{
							Color = (SharpDX.Color)((SharpDX.Direct2D1.SolidColorBrush)pocBrush).Color,
							Position = 0,
						},
						new SharpDX.Direct2D1.GradientStop()
						{
							Color = (SharpDX.Color)((SharpDX.Direct2D1.SolidColorBrush)bckBrush).Color,
							Position = 1,
						}
					}));
					
					RenderTarget.DrawRectangle(rect, gradBrush);
					
					gradBrush.Dispose();
				}
				*/
				#endregion

				#region Bar

				proBrush.Opacity = 0.6f;
				askBrush.Opacity = 0.6f;
				bidBrush.Opacity = 0.6f;

				if (bar)
				{
					if (profileWidth > 0f)
					{
						/// bar high

						x1 = rx - 3f;
						x2 = rx - profileWidth - barWidth - imbWidth;

						y1 = (chartScale.GetYByValue(profile.max) + chartScale.GetYByValue(profile.max + TickSize)) / 2;
						y2 = y1;

						vec1.X = x1;
						vec1.Y = y1;

						vec2.X = x2;
						vec2.Y = y2;

						if (profile.cls > profile.opn)
						{
							RenderTarget.DrawLine(vec1, vec2, askBrush, 1);
						}
						else if (profile.cls < profile.opn)
						{
							RenderTarget.DrawLine(vec1, vec2, bidBrush, 1);
						}
						else
						{
							RenderTarget.DrawLine(vec1, vec2, proBrush, 1);
						}

						/// bar low

						x1 = rx - 3f;
						x2 = rx - profileWidth - barWidth - imbWidth;

						y1 = (chartScale.GetYByValue(profile.min) + chartScale.GetYByValue(profile.min - TickSize)) / 2;
						y2 = y1;

						vec1.X = x1;
						vec1.Y = y1;

						vec2.X = x2;
						vec2.Y = y2;

						if (profile.cls > profile.opn)
						{
							RenderTarget.DrawLine(vec1, vec2, askBrush, 1);
						}
						else if (profile.cls < profile.opn)
						{
							RenderTarget.DrawLine(vec1, vec2, bidBrush, 1);
						}
						else
						{
							RenderTarget.DrawLine(vec1, vec2, proBrush, 1);
						}
					}

					/// bar - upper wick

					x1 = rx - barWidth + 3f;

					y1 = ((chartScale.GetYByValue(profile.max) + chartScale.GetYByValue(profile.max + TickSize)) / 2);
					y2 = chartScale.GetYByValue(Math.Max(profile.opn, profile.cls));

					vec1.X = x1;
					vec1.Y = y1;

					vec2.X = x1;
					vec2.Y = y2;

					if (profile.cls > profile.opn)
					{
						RenderTarget.DrawLine(vec1, vec2, askBrush, 1);
					}
					else if (profile.cls < profile.opn)
					{
						RenderTarget.DrawLine(vec1, vec2, bidBrush, 1);
					}
					else
					{
						RenderTarget.DrawLine(vec1, vec2, proBrush, 1);
					}

					/// bar - body

					x1 = rx;

					y1 = chartScale.GetYByValue(Math.Max(profile.opn, profile.cls));
					y2 = chartScale.GetYByValue(Math.Min(profile.opn, profile.cls));

					ht = Math.Abs(y2 - y1);

					rect.X = rx - barWidth + 1f;
					rect.Y = y1;
					rect.Width = 4f;
					rect.Height = ht;

					if (profile.cls > profile.opn)
					{
						RenderTarget.DrawRectangle(rect, askBrush);

						rect.Width -= 1f;
						rect.Height -= 1f;

						RenderTarget.FillRectangle(rect, askBrush);
					}

					if (profile.cls < profile.opn)
					{
						RenderTarget.DrawRectangle(rect, bidBrush);

						rect.Width -= 1f;
						rect.Height -= 1f;

						RenderTarget.FillRectangle(rect, bidBrush);
					}

					/// bar - lower wick

					x1 = rx - barWidth + 3f;

					y1 = chartScale.GetYByValue(Math.Min(profile.opn, profile.cls));
					y2 = ((chartScale.GetYByValue(profile.min) + chartScale.GetYByValue(profile.min - TickSize)) / 2);

					vec1.X = x1;
					vec1.Y = y1;

					vec2.X = x1;
					vec2.Y = y2;

					if (profile.cls > profile.opn)
					{
						RenderTarget.DrawLine(vec1, vec2, askBrush, 1);
					}
					else if (profile.cls < profile.opn)
					{
						RenderTarget.DrawLine(vec1, vec2, bidBrush, 1);
					}
					else
					{
						RenderTarget.DrawLine(vec1, vec2, proBrush, 1);
					}
				}
				else
				{
					x1 = rx - imbWidth - barWidth + 1f;
					x2 = rx - profileWidth - barWidth - imbWidth;

					y1 = ((chartScale.GetYByValue(profile.max) + chartScale.GetYByValue(profile.max + TickSize)) / 2);
					y2 = ((chartScale.GetYByValue(profile.min) + chartScale.GetYByValue(profile.min - TickSize)) / 2);

					/// high

					vec1.X = x1;
					vec1.Y = y1;

					vec2.X = x2;
					vec2.Y = y1;

					if (profile.dta > 0.0)
					{
						RenderTarget.DrawLine(vec1, vec2, askBrush, 1);
					}
					else if (profile.dta < 0.0)
					{
						RenderTarget.DrawLine(vec1, vec2, bidBrush, 1);
					}
					else
					{
						RenderTarget.DrawLine(vec1, vec2, proBrush, 1);
					}

					/// low

					vec1.X = x1;
					vec1.Y = y2;

					vec2.X = x2;
					vec2.Y = y2;

					if (profile.dta > 0.0)
					{
						RenderTarget.DrawLine(vec1, vec2, askBrush, 1);
					}
					else if (profile.dta < 0.0)
					{
						RenderTarget.DrawLine(vec1, vec2, bidBrush, 1);
					}
					else
					{
						RenderTarget.DrawLine(vec1, vec2, proBrush, 1);
					}

					/// ---

					vec1.X = x1;
					vec1.Y = y1;

					vec2.X = x1;
					vec2.Y = y2 - 1f;

					if (profile.dta > 0.0)
					{
						RenderTarget.DrawLine(vec1, vec2, askBrush, 1);
					}
					else if (profile.dta < 0.0)
					{
						RenderTarget.DrawLine(vec1, vec2, bidBrush, 1);
					}
					else
					{
						RenderTarget.DrawLine(vec1, vec2, proBrush, 1);
					}
				}

				#endregion

				#region Numbers

				if (showInfo)
				{
					tfNorm.TextAlignment = SharpDX.DirectWrite.TextAlignment.Trailing;
					tfNorm.WordWrapping = SharpDX.DirectWrite.WordWrapping.NoWrap;

					TextLayout textLayoutSum;
					TextLayout textLayoutDta;

					/// bot

					textLayoutSum = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, "Σ " + string.Format("{0:n0}", profile.vol), tfNorm, profileWidth, tfNorm.FontSize);
					textLayoutDta = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, "∆ " + string.Format("{0:n0}", profile.dta), tfNorm, profileWidth, tfNorm.FontSize);

					y1 = ((chartScale.GetYByValue(profile.min) + chartScale.GetYByValue(profile.min - TickSize)) / 2) + (float)(textLayoutSum.Metrics.Height / 2);

					RenderTarget.DrawTextLayout(new SharpDX.Vector2(rx - profileWidth - 6f, y1), textLayoutSum, proBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);

					y1 = y1 + textLayoutSum.Metrics.Height;

					if (profile.dta > 0.0)
					{
						RenderTarget.DrawTextLayout(new SharpDX.Vector2(rx - profileWidth - 6f, y1), textLayoutDta, askBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
					}
					else
					{
						RenderTarget.DrawTextLayout(new SharpDX.Vector2(rx - profileWidth - 6f, y1), textLayoutDta, bidBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
					}

					/// top

					y1 = ((chartScale.GetYByValue(profile.max) + chartScale.GetYByValue(profile.max + TickSize)) / 2) - (float)(textLayoutSum.Metrics.Height * 1.5);

					RenderTarget.DrawTextLayout(new SharpDX.Vector2(rx - profileWidth - 6f, y1), textLayoutSum, proBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);

					y1 = y1 - textLayoutSum.Metrics.Height;

					if (profile.dta > 0.0)
					{
						RenderTarget.DrawTextLayout(new SharpDX.Vector2(rx - profileWidth - 6f, y1), textLayoutDta, askBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
					}
					else
					{
						RenderTarget.DrawTextLayout(new SharpDX.Vector2(rx - profileWidth - 6f, y1), textLayoutDta, bidBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
					}

					textLayoutSum.Dispose();
					textLayoutDta.Dispose();
				}

				#endregion

				bor = (mh >= 3f) ? true : false;

				/// ---

				RenderTarget.AntialiasMode = oldAntialiasMode;
			}
			catch (Exception exception)
			{
				if (log)
				{
					NinjaTrader.Code.Output.Process(exception.ToString(), PrintTo.OutputTab1);
				}
			}
		}

		#endregion

		#region drawTapeStrip

		/// drawTapeStrip
		///
		private void drawTapeStrip(ChartControl chartControl, ChartScale chartScale)
		{
			if (ChartBars.ToIndex < ChartBars.Count - 1) { return; }

			try
			{
				if (!showTapeStrip) { return; }
				if (TapeStripItems.Count < 1) { return; }

				SharpDX.Direct2D1.AntialiasMode oldAntialiasMode = RenderTarget.AntialiasMode;
				RenderTarget.AntialiasMode = SharpDX.Direct2D1.AntialiasMode.Aliased;

				/// ---

				SharpDX.Vector2 vect = new SharpDX.Vector2();

				float x = chartControl.GetXByBarIndex(ChartBars, ChartBars.ToIndex) + cellWidth + 10f;
				x = (showFootprint && footprintDisplayType == FPV2FootprintDisplayType.Numbers && footprintDeltaProfile) ? x + (cellWidth + 2) : x;
				x = (showFootprint) ? x + 2f : x;

				float y = 0f;

				/// ---

				for (int i = 0; i < TapeStripItems.Count; i++)
				{
					y = (float)(chartScale.GetYByValue(TapeStripItems[i].prc));

					/// ---

					if (TapeStripItems[i].dir > 0)
					{
						askBrush.Opacity = 0.4f;

						x = drawLabel(TapeStripItems[i].vol.ToString(), false, x, y, askBrush, askBrush.Opacity, ntlBrush, 1f);
					}
					if (TapeStripItems[i].dir < 0)
					{
						bidBrush.Opacity = 0.4f;

						x = drawLabel(TapeStripItems[i].vol.ToString(), false, x, y, bidBrush, bidBrush.Opacity, ntlBrush, 1f);
					}

					x++;
				}

				/// ---

				RenderTarget.AntialiasMode = oldAntialiasMode;
			}
			catch (Exception exception)
			{
				if (log)
				{
					NinjaTrader.Code.Output.Process(exception.ToString(), PrintTo.OutputTab1);
				}
			}
		}

		#endregion

		#region drawMidas

		// drawMidas
		//
		private void drawMidas(ChartControl chartControl, ChartScale chartScale)
		{
			try
			{
				if (!showMidas) { return; }
				if (midasOpacity == 0.0f) { return; }
				if (MidasItems.Count < 1) { return; }

				SharpDX.Direct2D1.AntialiasMode oldAntialiasMode = RenderTarget.AntialiasMode;
				RenderTarget.AntialiasMode = SharpDX.Direct2D1.AntialiasMode.PerPrimitive;

				askBrush.Opacity = midasOpacity;
				bidBrush.Opacity = midasOpacity;

				SharpDX.Vector2 vec1 = new SharpDX.Vector2();
				SharpDX.Vector2 vec2 = new SharpDX.Vector2();

				int b1, b2;
				float x1, x2, y1, y2 = 0f;

				// ---

				double currValue = 0.0;
				double prevValue = 0.0;

				// ---

				for (int i = 0; i < MidasItems.Count; i++)
				{
					if (MidasItems[i].bar > ChartBars.ToIndex) { continue; }
					if (MidasItems[i].lst != 0 && MidasItems[i].lst < ChartBars.FromIndex) { continue; }
					if (onlyActive && !MidasItems[i].run) { continue; }

					for (int j = 1; j < MidasItems[i].vwap.Count; j++)
					{
						prevValue = MidasItems[i].vwap[j - 1];
						currValue = MidasItems[i].vwap[j];

						b1 = MidasItems[i].bar + j - 1;
						b2 = MidasItems[i].bar + j;

						if (prevValue > 0.0 && currValue > 0.0)
						{
							x1 = chartControl.GetXByBarIndex(ChartBars, b1);
							x2 = chartControl.GetXByBarIndex(ChartBars, b2);

							y1 = chartScale.GetYByValue(prevValue);
							y2 = chartScale.GetYByValue(currValue);

							vec1.X = x1;
							vec1.Y = y1;

							vec2.X = x2;
							vec2.Y = y2;

							if (MidasItems[i].dir > 0)
							{
								RenderTarget.DrawLine(vec1, vec2, bidBrush, midasLineWidth);
							}

							if (MidasItems[i].dir < 0)
							{
								RenderTarget.DrawLine(vec1, vec2, askBrush, midasLineWidth);
							}
						}
					}
				}

				// ---

				RenderTarget.AntialiasMode = oldAntialiasMode;
			}
			catch (Exception exception)
			{
				if (log)
				{
					NinjaTrader.Code.Output.Process(exception.ToString(), PrintTo.OutputTab1);
				}
			}
		}

		#endregion

		#region drawFootPrint

		/// drawFootPrint
		///
		private void drawFootPrint(ChartControl chartControl, ChartScale chartScale)
		{
			if (!showFootprint) { return; }

			SharpDX.Direct2D1.AntialiasMode oldAntialiasMode = RenderTarget.AntialiasMode;
			RenderTarget.AntialiasMode = SharpDX.Direct2D1.AntialiasMode.Aliased;

			SimpleFont sfDynNorm = null;
			SimpleFont sfDynBold = null;

			TextFormat tfDynNorm = null;
			TextFormat tfDynBold = null;

			if (dTextSize > 0)
			{
				sfDynNorm = new SimpleFont("Consolas", dTextSize);
				sfDynBold = new SimpleFont("Consolas", dTextSize) { Bold = true };

				tfDynNorm = sfDynNorm.ToDirectWriteTextFormat();
				tfDynBold = sfDynBold.ToDirectWriteTextFormat();
			}

			int x1 = 0;
			int x2 = 0;
			float y1 = 0;
			float y2 = 0;
			float tx = 0;
			float ty = 0;

			double poc = 0.0;
			double dtc = 0.0;
			double prc = BarsArray[0].GetClose(ChartBars.ToIndex);

			double askVol;
			double bidVol;
			double dtaVol;
			double maxVol = 0.0;
			double tmpVol = 0.0;
			double maxDta = 0.0;
			double tmpDta = 0.0;
			double barWidth;

			float oRng = footprintMaxOpa;
			float opac = oRng;

			TextLayout tl;

			NinjaTrader.NinjaScript.Indicators.Infinity.BarData.BarItem currBarItem;

			SharpDX.RectangleF rect = new SharpDX.RectangleF();
			SharpDX.Vector2 vec1 = new SharpDX.Vector2();
			SharpDX.Vector2 vec2 = new SharpDX.Vector2();

			/// ---

			if (footprintRelativeVolume)
			{
				for (int i = ChartBars.FromIndex; i <= ChartBars.ToIndex; i++)
				{
					if (barData.BarItems.IsValidDataPointAt(i))
					{
						currBarItem = barData.BarItems.GetValueAt(i);
					}
					else
					{
						continue;
					}

					if (currBarItem == null) { continue; }
					if (currBarItem.rowItems.IsEmpty) { continue; }

					tmpVol = currBarItem.getMaxVol();
					maxVol = (tmpVol > maxVol) ? tmpVol : maxVol;

					if (footprintDisplayType == FPV2FootprintDisplayType.Numbers && footprintDeltaProfile)
					{
						tmpDta = currBarItem.getMaxDta();
						maxDta = (tmpDta > maxDta) ? tmpDta : maxDta;
					}
				}
			}

			/// ---

			for (int i = ChartBars.FromIndex; i <= ChartBars.ToIndex; i++)
			{
				if (barData.BarItems.IsValidDataPointAt(i))
				{
					currBarItem = barData.BarItems.GetValueAt(i);
				}
				else
				{
					continue;
				}

				if (currBarItem == null) { continue; }
				if (currBarItem.rowItems.IsEmpty) { continue; }

				x1 = chartControl.GetXByBarIndex(ChartBars, i);
				x2 = x1 + 20;

				poc = currBarItem.poc;
				dtc = 0.0;

				if (!footprintRelativeVolume)
				{
					maxVol = currBarItem.getMaxVol();

					if (footprintDisplayType == FPV2FootprintDisplayType.Numbers && footprintDeltaProfile)
					{
						maxDta = currBarItem.getMaxDta();
					}
				}

				/// draw background box

				y1 = ((chartScale.GetYByValue(currBarItem.max) + chartScale.GetYByValue(currBarItem.max + TickSize)) / 2) + 1;
				y2 = ((chartScale.GetYByValue(currBarItem.min) + chartScale.GetYByValue(currBarItem.min - TickSize)) / 2) - 1;

				rect.X = (float)(x1 - barHalfWidth - cellWidth) - 1f;
				rect.Y = (float)y1 - 1f;
				rect.Width = (float)((barHalfWidth * 2) + (cellWidth * 2)) + 1f;
				rect.Height = (float)Math.Abs(y1 - y2) + 1f;

				#region delta outline

				if (footprintDeltaOutline)
				{
					// outline background

					if (showFootprint && !custProfileMap)
					{
						if (currBarItem.dtc > 0.0)
						{
							askBrush.Opacity = 0.04f;
							RenderTarget.FillRectangle(rect, askBrush);
						}
						else if (currBarItem.dtc < 0.0)
						{
							bidBrush.Opacity = 0.04f;
							RenderTarget.FillRectangle(rect, bidBrush);
						}
						else
						{
							proBrush.Opacity = 0.04f;
							RenderTarget.FillRectangle(rect, proBrush);
						}
					}

					/// ---

					// upper outline

					vec1.X = rect.X;
					vec1.Y = y1 - 2;

					vec2.X = rect.X + rect.Width;
					vec2.Y = y1 - 2;

					if (currBarItem.dtc < 0.0)
					{
						bidBrush.Opacity = 0.6f;
						RenderTarget.DrawLine(vec1, vec2, bidBrush, 1);
					}
					else
					{
						askBrush.Opacity = 0.6f;
						RenderTarget.DrawLine(vec1, vec2, askBrush, 1);
					}

					// lower outline

					vec1.X = rect.X;
					vec1.Y = y2 + 2;

					vec2.X = rect.X + rect.Width;
					vec2.Y = y2 + 2;

					if (currBarItem.dtc < 0.0)
					{
						bidBrush.Opacity = 0.6f;
						RenderTarget.DrawLine(vec1, vec2, bidBrush, 1);
					}
					else
					{
						askBrush.Opacity = 0.6f;
						RenderTarget.DrawLine(vec1, vec2, askBrush, 1);
					}
				}
				else
				{
					// outline background

					if (showFootprint && !custProfileMap)
					{
						proBrush.Opacity = 0.04f;
						RenderTarget.FillRectangle(rect, proBrush);
					}
				}

				#endregion

				#region fill empty prices

				if (BarsArray[0].GetHigh(i) > currBarItem.max)
				{
					double currPrc = BarsArray[0].GetHigh(i);

					while (currPrc > currBarItem.max)
					{
						currBarItem.addVol(currPrc, 0, prevProfileShow, currProfileShow, custProfileShow);
						currPrc -= TickSize;
					}
				}

				if (BarsArray[0].GetLow(i) < currBarItem.min)
				{
					double currPrc = BarsArray[0].GetLow(i);

					while (currPrc < currBarItem.min)
					{
						currBarItem.addVol(currPrc, 0, prevProfileShow, currProfileShow, custProfileShow);
						currPrc += TickSize;
					}
				}

				#endregion

				foreach (KeyValuePair<double, NinjaTrader.NinjaScript.Indicators.Infinity.BarData.RowItem> ri in currBarItem.rowItems)
				{
					askVol = ri.Value.ask;
					bidVol = ri.Value.bid;
					dtaVol = ri.Value.dta;

					dtc += dtaVol;

					y1 = ((chartScale.GetYByValue(ri.Key) + chartScale.GetYByValue(ri.Key + TickSize)) / 2) + 1;
					y2 = ((chartScale.GetYByValue(ri.Key) + chartScale.GetYByValue(ri.Key - TickSize)) / 2) - 1;

					#region ask cell

					/// 
					/// - ask cell ------------------------------------------------------------------------------
					///

					bool isAskImbalance = (footprintImbalances) ? currBarItem.isAskImbalance(ri.Key, ri.Key - TickSize, minImbalanceRatio) : false;

					/// ---

					barWidth = (footprintDisplayType == FPV2FootprintDisplayType.Numbers) ? cellWidth : (cellWidth / maxVol) * askVol;
					barWidth = Math.Round(barWidth);

					rect.X = (float)(x1 + barHalfWidth);
					rect.Y = (float)y1;
					rect.Width = (float)barWidth;
					rect.Height = (float)Math.Abs(y1 - y2);

					/// ---

					bckBrush.Opacity = 1.0f;

					RenderTarget.DrawRectangle(rect, bckBrush);
					RenderTarget.FillRectangle(rect, bckBrush);

					/// ---

					if (footprintDisplayType == FPV2FootprintDisplayType.Numbers)
					{
						opac = (footprintGradient) ? (float)Math.Round((oRng / maxVol) * ri.Value.ask, 5) : 0.15f;
						opac = (!footprintGradient && ri.Key == poc) ? opac + 0.25f : opac;
						opac = (!footprintGradient && i == ChartBars.ToIndex && GetCurrentAsk() == ri.Key && ri.Key == prc) ? opac + 0.1f : opac;

						proBrush.Opacity = opac;

						RenderTarget.DrawRectangle(rect, proBrush);

						rect.Width = rect.Width - 1;
						rect.Height = rect.Height - 1;

						RenderTarget.FillRectangle(rect, proBrush);

						rect.Width = rect.Width + 1;
						rect.Height = rect.Height + 1;
					}
					else
					{
						opac = (footprintGradient) ? (float)Math.Round((oRng / maxVol) * ri.Value.ask, 5) + 0.04f : footprintMaxOpa;

						rect.Width = (float)barWidth;

						if (!footprintGradient && ri.Key == poc)
						{
							if (isAskImbalance)
							{
								askBrush.Opacity = Math.Min(1.0f, opac + 0.4f);
								RenderTarget.DrawRectangle(rect, askBrush);

								rect.Width = rect.Width - 1;
								rect.Height = rect.Height - 1;

								askBrush.Opacity = Math.Min(1.0f, opac + 0.4f);
								RenderTarget.FillRectangle(rect, askBrush);
							}
							else
							{
								proBrush.Opacity = Math.Min(1.0f, opac + 0.4f);
								RenderTarget.DrawRectangle(rect, proBrush);

								rect.Width = rect.Width - 1;
								rect.Height = rect.Height - 1;

								proBrush.Opacity = Math.Min(1.0f, opac + 0.4f);
								RenderTarget.FillRectangle(rect, proBrush);
							}
						}
						else if (isAskImbalance)
						{
							askBrush.Opacity = (footprintGradient) ? Math.Min(1.0f, opac + 0.2f) : Math.Min(1.0f, opac + 0.2f);
							RenderTarget.DrawRectangle(rect, askBrush);

							rect.Width = rect.Width - 1;
							rect.Height = rect.Height - 1;

							if (footprintGradient)
							{
								askBrush.Opacity = opac;
								RenderTarget.FillRectangle(rect, askBrush);
							}
							else
							{
								askBrush.Opacity = Math.Min(1.0f, opac + 0.2f);
								RenderTarget.FillRectangle(rect, askBrush);
							}
						}
						else
						{
							proBrush.Opacity = opac;
							RenderTarget.DrawRectangle(rect, proBrush);

							rect.Width = rect.Width - 1;
							rect.Height = rect.Height - 1;

							RenderTarget.FillRectangle(rect, proBrush);
						}

						rect.Width = rect.Width + 1;
						rect.Height = rect.Height + 1;
					}

					/// poc & imbalance

					if (footprintDisplayType == FPV2FootprintDisplayType.Numbers)
					{
						if (ri.Key == poc)
						{
							proBrush.Opacity = 1.0f;

							vec1.X = rect.X + cellWidth - 1f;
							vec1.Y = y1 - 1f;

							vec2.X = rect.X + cellWidth - 1f;
							vec2.Y = y2;

							RenderTarget.DrawLine(vec1, vec2, proBrush, 3);
						}
						/*
						if(isAskImbalance)
						{
							askBrush.Opacity = 1.0f;
							
							vec1.X = rect.X + cellWidth;
							vec1.Y = y1 - 1f;
							
							vec2.X = rect.X + cellWidth;
							vec2.Y = y2;
							
							RenderTarget.DrawLine(vec1, vec2, askBrush, 1);
						}
						*/
					}

					/// ask - text

					if (footprintDisplayType == FPV2FootprintDisplayType.Numbers && tfDynNorm != null && tfDynBold != null)
					{
						tfDynNorm.TextAlignment = SharpDX.DirectWrite.TextAlignment.Leading;
						tfDynBold.TextAlignment = SharpDX.DirectWrite.TextAlignment.Leading;

						tl = (ri.Key == poc) ? new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, askVol.ToString(), tfDynBold, rect.Width, rect.Height) : new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, askVol.ToString(), tfDynNorm, rect.Width, rect.Height);

						tl.ParagraphAlignment = SharpDX.DirectWrite.ParagraphAlignment.Center;

						/// ---

						vec1.X = rect.X + 6f;
						vec1.Y = rect.Y - 1f;

						/// ---

						opac = (footprintGradient) ? (float)Math.Round((oRng / maxVol) * ri.Value.ask, 5) + 0.6f : 0.75f;

						if (isAskImbalance)
						{
							opac = 1.0f;
						}

						if (!footprintGradient && ri.Key == poc)
						{
							opac = 1.0f;
						}

						opac = Math.Min(1.0f, opac);

						/// ---

						if (isAskImbalance)
						{
							askBrush.Opacity = opac;
							RenderTarget.DrawTextLayout(vec1, tl, askBrush);
						}
						else
						{
							proBrush.Opacity = opac;
							RenderTarget.DrawTextLayout(vec1, tl, proBrush);
						}

						/// ---

						tl.Dispose();
					}

					#endregion

					#region delta profile

					if (footprintDisplayType == FPV2FootprintDisplayType.Numbers && footprintDeltaProfile)
					{
						barWidth = (cellWidth / maxDta) * Math.Abs(dtaVol);
						barWidth = Math.Round(barWidth);

						if (barWidth >= 1)
						{
							rect.X = (float)(x1 + barHalfWidth + cellWidth + 2);
							rect.Y = (float)y1;
							rect.Width = (float)barWidth;
							rect.Height = (float)Math.Abs(y1 - y2);

							/// ---

							bckBrush.Opacity = 1.0f;

							RenderTarget.DrawRectangle(rect, bckBrush);
							RenderTarget.FillRectangle(rect, bckBrush);

							/// ---

							opac = (footprintDeltaGradient) ? (float)((0.5f / maxDta) * Math.Abs(dtaVol)) + 0.1f : 0.5f;

							if (dtaVol > 0.0)
							{
								askBrush.Opacity = opac;
								RenderTarget.DrawRectangle(rect, askBrush);

								rect.Width = rect.Width - 1;
								rect.Height = rect.Height - 1;

								RenderTarget.FillRectangle(rect, askBrush);
							}
							if (dtaVol < 0.0)
							{
								bidBrush.Opacity = opac;
								RenderTarget.DrawRectangle(rect, bidBrush);

								rect.Width = rect.Width - 1;
								rect.Height = rect.Height - 1;

								RenderTarget.FillRectangle(rect, bidBrush);
							}
						}
					}

					#endregion

					#region bid cell

					/// 
					/// - bid cell ------------------------------------------------------------------------------
					///

					bool isBidImbalance = (footprintImbalances) ? currBarItem.isBidImbalance(ri.Key + TickSize, ri.Key, minImbalanceRatio) : false;

					/// ---

					barWidth = (footprintDisplayType == FPV2FootprintDisplayType.Numbers) ? cellWidth : (cellWidth / maxVol) * bidVol;
					barWidth = Math.Round(barWidth);

					rect.X = (float)(x1 - barHalfWidth - barWidth);
					rect.Y = (float)y1;
					rect.Width = (float)barWidth;
					rect.Height = (float)Math.Abs(y1 - y2);

					/// ---

					bckBrush.Opacity = 1.0f;

					RenderTarget.DrawRectangle(rect, bckBrush);
					RenderTarget.FillRectangle(rect, bckBrush);

					/// ---

					if (footprintDisplayType == FPV2FootprintDisplayType.Numbers)
					{
						opac = (footprintGradient) ? (float)Math.Round((oRng / maxVol) * ri.Value.bid, 5) : 0.15f;
						opac = (!footprintGradient && ri.Key == poc) ? opac + 0.25f : opac;
						opac = (!footprintGradient && i == ChartBars.ToIndex && GetCurrentBid() == ri.Key && ri.Key == prc) ? opac + 0.1f : opac;

						proBrush.Opacity = opac;

						RenderTarget.DrawRectangle(rect, proBrush);

						rect.Width = rect.Width - 1;
						rect.Height = rect.Height - 1;

						RenderTarget.FillRectangle(rect, proBrush);

						rect.Width = rect.Width + 1;
						rect.Height = rect.Height + 1;
					}
					else
					{
						opac = (footprintGradient) ? (float)Math.Round((oRng / maxVol) * ri.Value.bid, 5) + 0.04f : footprintMaxOpa;

						rect.Width = (float)barWidth;

						if (!footprintGradient && ri.Key == poc)
						{
							if (isBidImbalance)
							{
								bidBrush.Opacity = Math.Min(1.0f, opac + 0.4f);
								RenderTarget.DrawRectangle(rect, bidBrush);

								rect.Width = rect.Width - 1;
								rect.Height = rect.Height - 1;

								bidBrush.Opacity = Math.Min(1.0f, opac + 0.4f);
								RenderTarget.FillRectangle(rect, bidBrush);
							}
							else
							{
								proBrush.Opacity = Math.Min(1.0f, opac + 0.4f);
								RenderTarget.DrawRectangle(rect, proBrush);

								rect.Width = rect.Width - 1;
								rect.Height = rect.Height - 1;

								RenderTarget.FillRectangle(rect, proBrush);
							}
						}
						else if (isBidImbalance)
						{
							bidBrush.Opacity = (footprintGradient) ? Math.Min(1.0f, opac + 0.2f) : Math.Min(1.0f, opac + 0.2f);
							RenderTarget.DrawRectangle(rect, bidBrush);

							rect.Width = rect.Width - 1;
							rect.Height = rect.Height - 1;

							if (footprintGradient)
							{
								bidBrush.Opacity = opac;
								RenderTarget.FillRectangle(rect, bidBrush);
							}
							else
							{
								bidBrush.Opacity = Math.Min(1.0f, opac + 0.2f);
								RenderTarget.FillRectangle(rect, bidBrush);
							}
						}
						else
						{
							proBrush.Opacity = opac;
							RenderTarget.DrawRectangle(rect, proBrush);

							rect.Width = rect.Width - 1;
							rect.Height = rect.Height - 1;

							RenderTarget.FillRectangle(rect, proBrush);
						}

						rect.Width = rect.Width + 1;
						rect.Height = rect.Height + 1;
					}

					/// poc & imbalance

					if (footprintDisplayType == FPV2FootprintDisplayType.Numbers)
					{
						if (ri.Key == poc)
						{
							proBrush.Opacity = 1.0f;

							vec1.X = rect.X + 1f;
							vec1.Y = y1 - 1f;

							vec2.X = rect.X + 1f;
							vec2.Y = y2;

							RenderTarget.DrawLine(vec1, vec2, proBrush, 3);
						}
						/*
						if(isBidImbalance)
						{
							bidBrush.Opacity = 1.0f;
							
							vec1.X = rect.X;
							vec1.Y = y1 - 1f;
							
							vec2.X = rect.X;
							vec2.Y = y2;
							
							RenderTarget.DrawLine(vec1, vec2, bidBrush, 1);
						}
						*/
					}

					/// bid - text

					if (footprintDisplayType == FPV2FootprintDisplayType.Numbers && tfDynNorm != null && tfDynBold != null)
					{
						tfDynNorm.TextAlignment = SharpDX.DirectWrite.TextAlignment.Trailing;
						tfDynBold.TextAlignment = SharpDX.DirectWrite.TextAlignment.Trailing;

						tl = (ri.Key == poc) ? new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, bidVol.ToString(), tfDynBold, rect.Width, rect.Height) : new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, bidVol.ToString(), tfDynNorm, rect.Width, rect.Height);

						tl.ParagraphAlignment = SharpDX.DirectWrite.ParagraphAlignment.Center;

						/// ---

						vec1.X = rect.X - 7f;
						vec1.Y = rect.Y - 1f;

						/// ---

						opac = (footprintGradient) ? (float)Math.Round((oRng / maxVol) * ri.Value.bid, 5) + 0.6f : 0.75f;

						if (isBidImbalance)
						{
							opac = 1.0f;
						}

						if (!footprintGradient && ri.Key == poc)
						{
							opac = 1.0f;
						}

						opac = Math.Min(1.0f, opac);

						/// ---

						if (isBidImbalance)
						{
							bidBrush.Opacity = opac;
							RenderTarget.DrawTextLayout(vec1, tl, bidBrush);
						}
						else
						{
							proBrush.Opacity = opac;
							RenderTarget.DrawTextLayout(vec1, tl, proBrush);
						}

						/// ---

						tl.Dispose();
					}

					#endregion
				}
			}

			/// ---

			currBarItem = null;

			/// ---

			if (tfDynNorm != null && tfDynBold != null)
			{
				sfDynNorm = null;
				sfDynBold = null;
				tfDynNorm.Dispose();
				tfDynBold.Dispose();
			}

			/// ---

			RenderTarget.AntialiasMode = oldAntialiasMode;
		}

		#endregion

		#region drawFootPrintBarInfo

		/// drawFootPrintBarInfo
		///
		private void drawFootPrintBarInfo(ChartControl chartControl, ChartScale chartScale)
		{
			if (!showFootprint) { return; }
			if (!footprintBarVolume && !footprintBarDelta && !footprintBarDeltaSwing) { return; }

			SharpDX.Direct2D1.AntialiasMode oldAntialiasMode = RenderTarget.AntialiasMode;
			RenderTarget.AntialiasMode = SharpDX.Direct2D1.AntialiasMode.Aliased;

			int x1 = 0;
			int x2 = 0;
			float y1 = 0;
			float y2 = 0;

			string returns;

			NinjaTrader.NinjaScript.Indicators.Infinity.BarData.BarItem currBarItem;

			SharpDX.RectangleF rect = new SharpDX.RectangleF();

			/// ---

			for (int i = ChartBars.FromIndex; i <= ChartBars.ToIndex; i++)
			{
				if (barData.BarItems.IsValidDataPointAt(i))
				{
					currBarItem = barData.BarItems.GetValueAt(i);
				}
				else
				{
					continue;
				}

				if (currBarItem == null) { continue; }
				if (currBarItem.rowItems.IsEmpty) { continue; }

				/// ---

				x1 = chartControl.GetXByBarIndex(ChartBars, i);
				x2 = x1 + 20;

				y1 = ((chartScale.GetYByValue(currBarItem.max) + chartScale.GetYByValue(currBarItem.max + TickSize)) / 2) + 1;
				y2 = ((chartScale.GetYByValue(currBarItem.min) + chartScale.GetYByValue(currBarItem.min - TickSize)) / 2) - 1;

				rect.X = (float)(x1 - barHalfWidth - cellWidth) - 1f;
				rect.Y = (float)y1 - 1f;
				rect.Width = (float)((barHalfWidth * 2) + (cellWidth * 2)) + 1f;
				rect.Height = (float)Math.Abs(y1 - y2) + 1f;

				/// ---

				tfNorm.TextAlignment = SharpDX.DirectWrite.TextAlignment.Center;
				tfNorm.WordWrapping = SharpDX.DirectWrite.WordWrapping.NoWrap;

				tfBold.TextAlignment = SharpDX.DirectWrite.TextAlignment.Center;
				tfBold.WordWrapping = SharpDX.DirectWrite.WordWrapping.NoWrap;

				// upper text

				returns = "";

				if (footprintBarVolume)
				{
					String volText = "Σ " + string.Format("{0:n0}", currBarItem.vol);

					TextLayout textLayoutVol = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, volText, tfNorm, rect.Width, tfNorm.FontSize);

					proBrush.Opacity = 0.6f;
					RenderTarget.DrawTextLayout(new SharpDX.Vector2(rect.X, rect.Y - textLayoutVol.Metrics.Height - tfNorm.FontSize * 0.5f), textLayoutVol, proBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);

					returns += "\n";

					volText = null;
					textLayoutVol.Dispose();
				}

				if (footprintBarDelta)
				{
					String dtaText = "∆ " + string.Format("{0:n0}", currBarItem.dtc) + returns;

					TextLayout textLayoutDta = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, dtaText, tfNorm, rect.Width, tfNorm.FontSize);

					if (currBarItem.dtc < 0.0)
					{
						bidBrush.Opacity = 0.6f;
						RenderTarget.DrawTextLayout(new SharpDX.Vector2(rect.X, rect.Y - textLayoutDta.Metrics.Height - tfNorm.FontSize * 0.5f), textLayoutDta, bidBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
					}
					else if (currBarItem.dtc > 0.0)
					{
						askBrush.Opacity = 0.6f;
						RenderTarget.DrawTextLayout(new SharpDX.Vector2(rect.X, rect.Y - textLayoutDta.Metrics.Height - tfNorm.FontSize * 0.5f), textLayoutDta, askBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
					}
					else
					{
						proBrush.Opacity = 0.6f;
						RenderTarget.DrawTextLayout(new SharpDX.Vector2(rect.X, rect.Y - textLayoutDta.Metrics.Height - tfNorm.FontSize * 0.5f), textLayoutDta, proBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
					}

					returns += "\n";

					dtaText = null;
					textLayoutDta.Dispose();
				}

				if (footprintBarDeltaSwing)
				{
					if (ZigZagDots.IsValidDataPointAt(i))
					{
						if (ZigZagDots.GetValueAt(i) == BarsArray[0].GetHigh(i))
						{
							String cumText = "∆ " + string.Format("{0:n0}", currBarItem.cdh) + returns;

							TextLayout textLayoutCum = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, cumText, tfBold, rect.Width, tfBold.FontSize);

							if (currBarItem.cdh < 0.0)
							{
								bidBrush.Opacity = 0.6f;
								RenderTarget.DrawTextLayout(new SharpDX.Vector2(rect.X, rect.Y - textLayoutCum.Metrics.Height - tfBold.FontSize * 0.5f), textLayoutCum, bidBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
							}
							else if (currBarItem.cdh > 0.0)
							{
								askBrush.Opacity = 0.6f;
								RenderTarget.DrawTextLayout(new SharpDX.Vector2(rect.X, rect.Y - textLayoutCum.Metrics.Height - tfBold.FontSize * 0.5f), textLayoutCum, askBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
							}
							else
							{
								proBrush.Opacity = 0.6f;
								RenderTarget.DrawTextLayout(new SharpDX.Vector2(rect.X, rect.Y - textLayoutCum.Metrics.Height - tfBold.FontSize * 0.5f), textLayoutCum, proBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
							}

							cumText = null;
							textLayoutCum.Dispose();
						}
					}
				}

				// lower text

				returns = "";

				if (footprintBarVolume)
				{
					String volText = "Σ " + string.Format("{0:n0}", currBarItem.vol);

					TextLayout textLayoutVol = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, volText, tfNorm, rect.Width, tfNorm.FontSize);

					proBrush.Opacity = 0.6f;
					RenderTarget.DrawTextLayout(new SharpDX.Vector2(rect.X, rect.Y + rect.Height + tfNorm.FontSize * 0.5f), textLayoutVol, proBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);

					returns += "\n";

					volText = null;
					textLayoutVol.Dispose();
				}

				if (footprintBarDelta)
				{
					String dtaText = returns + "∆ " + string.Format("{0:n0}", currBarItem.dtc);

					TextLayout textLayoutDta = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, dtaText, tfNorm, rect.Width, tfNorm.FontSize);

					if (currBarItem.dtc < 0.0)
					{
						bidBrush.Opacity = 0.6f;
						RenderTarget.DrawTextLayout(new SharpDX.Vector2(rect.X, rect.Y + rect.Height + tfNorm.FontSize * 0.5f), textLayoutDta, bidBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
					}
					else if (currBarItem.dtc > 0.0)
					{
						askBrush.Opacity = 0.6f;
						RenderTarget.DrawTextLayout(new SharpDX.Vector2(rect.X, rect.Y + rect.Height + tfNorm.FontSize * 0.5f), textLayoutDta, askBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
					}
					else
					{
						proBrush.Opacity = 0.6f;
						RenderTarget.DrawTextLayout(new SharpDX.Vector2(rect.X, rect.Y + rect.Height + tfNorm.FontSize * 0.5f), textLayoutDta, proBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
					}

					returns += "\n";

					dtaText = null;
					textLayoutDta.Dispose();
				}

				if (footprintBarDeltaSwing)
				{
					if (ZigZagDots.IsValidDataPointAt(i))
					{
						if (ZigZagDots.GetValueAt(i) == BarsArray[0].GetLow(i))
						{
							String cumText = returns + "∆ " + string.Format("{0:n0}", currBarItem.cdl);

							TextLayout textLayoutCum = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, cumText, tfBold, rect.Width, tfBold.FontSize);

							if (currBarItem.cdl < 0.0)
							{
								bidBrush.Opacity = 0.6f;
								RenderTarget.DrawTextLayout(new SharpDX.Vector2(rect.X, rect.Y + rect.Height + tfBold.FontSize * 0.5f), textLayoutCum, bidBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
							}
							else if (currBarItem.cdl > 0.0)
							{
								askBrush.Opacity = 0.6f;
								RenderTarget.DrawTextLayout(new SharpDX.Vector2(rect.X, rect.Y + rect.Height + tfBold.FontSize * 0.5f), textLayoutCum, askBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
							}
							else
							{
								proBrush.Opacity = 0.6f;
								RenderTarget.DrawTextLayout(new SharpDX.Vector2(rect.X, rect.Y + rect.Height + tfBold.FontSize * 0.5f), textLayoutCum, proBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
							}

							cumText = null;
							textLayoutCum.Dispose();
						}
					}
				}
			}

			/// ---

			currBarItem = null;

			/// ---

			RenderTarget.AntialiasMode = oldAntialiasMode;
		}

		#endregion

		#region drawPoc

		/// drawPoc
		///
		private void drawPoc(ChartControl chartControl, ChartScale chartScale)
		{
			if (showFootprint || !showPocOnBarChart) { return; }

			SharpDX.Direct2D1.AntialiasMode oldAntialiasMode = RenderTarget.AntialiasMode;
			RenderTarget.AntialiasMode = SharpDX.Direct2D1.AntialiasMode.Aliased;

			SharpDX.Vector2 vec1 = new SharpDX.Vector2();
			SharpDX.Vector2 vec2 = new SharpDX.Vector2();

			int x1, x2;
			float y1, y2;

			NinjaTrader.NinjaScript.Indicators.Infinity.BarData.BarItem currBarItem;

			for (int i = ChartBars.FromIndex; i <= ChartBars.ToIndex; i++)
			{
				if (barData.BarItems.IsValidDataPointAt(i))
				{
					currBarItem = barData.BarItems.GetValueAt(i);
				}
				else
				{
					continue;
				}

				if (currBarItem == null) { continue; }
				if (currBarItem.rowItems.IsEmpty) { continue; }

				x1 = chartControl.GetXByBarIndex(ChartBars, i) - (barFullWidth / 2) - 1;
				x2 = x1 + barFullWidth;

				y1 = chartScale.GetYByValue(currBarItem.poc);
				y2 = y1;

				vec1.X = x1;
				vec1.Y = y1;

				vec2.X = x2;
				vec2.Y = y2;

				proBrush.Opacity = 1f;
				RenderTarget.DrawLine(vec1, vec2, proBrush, 2);
			}

			RenderTarget.AntialiasMode = oldAntialiasMode;
		}

		#endregion

		#region drawBottomArea

		/// drawBottomArea
		///
		private void drawBottomArea(ChartControl chartControl, ChartScale chartScale)
		{
			if (!showBottomArea && !showFootprint) { return; }

			int barDistance = (int)(((cellWidth + barHalfWidth) * 2) + 5);

			/// ---

			SharpDX.Direct2D1.AntialiasMode oldAntialiasMode = RenderTarget.AntialiasMode;
			RenderTarget.AntialiasMode = SharpDX.Direct2D1.AntialiasMode.Aliased;

			ChartPanel chartPanel = chartControl.ChartPanels[chartScale.PanelIndex];

			NinjaTrader.NinjaScript.Indicators.Infinity.BarData.BarItem currBarItem;

			SharpDX.RectangleF rect = new SharpDX.RectangleF();
			SharpDX.Vector2 vec1 = new SharpDX.Vector2();
			SharpDX.Vector2 vec2 = new SharpDX.Vector2();

			/// ---

			double minPrc = double.MaxValue;
			double maxPrc = double.MinValue;
			double prcRng = 0.0;
			float pixRng = 0f;
			float maxPix = 0f;
			float ticPix = chartScale.GetPixelsForDistance(TickSize);

			for (int i = ChartBars.FromIndex; i <= ChartBars.ToIndex; i++)
			{
				minPrc = (BarsArray[0].GetLow(i) < minPrc) ? BarsArray[0].GetLow(i) : minPrc;
				maxPrc = (BarsArray[0].GetHigh(i) > maxPrc) ? BarsArray[0].GetHigh(i) : maxPrc;
			}

			prcRng = (chartScale.Properties.YAxisRangeType == YAxisRangeType.Fixed) ? chartScale.MaxMinusMin : maxPrc - minPrc;
			pixRng = chartScale.GetPixelsForDistance(prcRng);
			maxPix = (float)((pixRng / 100.0f) * chartScale.Properties.AutoScaleMarginLower);
			maxPix -= (showFootprint) ? ticPix / 2.0f : 1f;
			maxPix -= (showFootprint && footprintDeltaOutline) ? (tfNorm.FontSize * 2.0f) : 0f;

			/// ---

			int x1 = 0;
			int x2 = 0;
			float xl = 0f;
			float y1 = 0f;
			float y2 = 0f;
			float wt = 0f;
			float ht = 0f;
			float offset = 0f;
			float opacity = 0f;
			double maxDtc = double.MinValue;
			double minDta = double.MaxValue;
			double maxDta = double.MinValue;
			double minCdc = double.MaxValue;
			double maxCdc = double.MinValue;
			double maxVol = 0.0;
			double dtaRng = 0.0;
			double cdcRng = 0.0;
			double barWid = chartControl.GetBarPaintWidth(chartControl.BarsArray[0]);
			double factor = 0.0;

			/// ---

			for (int i = ChartBars.FromIndex; i <= ChartBars.ToIndex; i++)
			{
				if (barData.BarItems.IsValidDataPointAt(i))
				{
					currBarItem = barData.BarItems.GetValueAt(i);
				}
				else
				{
					continue;
				}

				if (currBarItem == null) { continue; }
				if (currBarItem.rowItems.IsEmpty) { continue; }

				maxVol = (Math.Abs(currBarItem.vol) > maxVol) ? Math.Abs(currBarItem.vol) : maxVol;
				maxDtc = (Math.Abs(currBarItem.dtc) > maxDtc) ? Math.Abs(currBarItem.dtc) : maxDtc;
				minCdc = (Math.Abs(currBarItem.cdc) < minCdc) ? Math.Abs(currBarItem.cdc) : minCdc;
				maxCdc = (Math.Abs(currBarItem.cdc) > maxCdc) ? Math.Abs(currBarItem.cdc) : maxCdc;
				minDta = (currBarItem.cdl < minDta) ? currBarItem.cdl : minDta;
				maxDta = (currBarItem.cdh > maxDta) ? currBarItem.cdh : maxDta;
			}

			cdcRng = maxCdc - minCdc;
			dtaRng = Math.Abs(maxDta - minDta);

			if (showFootprint)
			{
				#region cumulative delta text

				if (bottomTextCumulativeDelta)
				{
					for (int i = ChartBars.FromIndex; i <= ChartBars.ToIndex; i++)
					{
						if (barData.BarItems.IsValidDataPointAt(i))
						{
							currBarItem = barData.BarItems.GetValueAt(i);
						}
						else
						{
							continue;
						}

						if (currBarItem == null) { continue; }
						if (currBarItem.dtc == 0) { continue; }

						x1 = chartControl.GetXByBarIndex(ChartBars, i);
						ht = tfNorm.FontSize + 4f;

						rect.X = (float)(x1 - barHalfWidth - cellWidth) - 1f;
						rect.Y = (float)(chartPanel.H - ht - offset);
						rect.Width = (float)((barHalfWidth * 2) + (cellWidth * 2)) + 1f;
						rect.Height = ht;

						bckBrush.Opacity = 1.0f;
						RenderTarget.FillRectangle(rect, bckBrush);

						opacity = (bottomAreaGradient) ? (float)Math.Max((0.4f / cdcRng) * (Math.Abs(currBarItem.cdc) - minCdc), 0.05f) : 0.4f;

						if (currBarItem.cdc > 0.0)
						{
							askBrush.Opacity = opacity;
							RenderTarget.FillRectangle(rect, askBrush);
						}
						else if (currBarItem.cdc < 0.0)
						{
							bidBrush.Opacity = opacity;
							RenderTarget.FillRectangle(rect, bidBrush);
						}
						else
						{
							ntlBrush.Opacity = opacity;
							RenderTarget.FillRectangle(rect, ntlBrush);
						}

						tfNorm.TextAlignment = SharpDX.DirectWrite.TextAlignment.Center;
						tfNorm.WordWrapping = SharpDX.DirectWrite.WordWrapping.NoWrap;

						TextLayout tl = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, string.Format("{0:n0}", Math.Abs(currBarItem.cdc)), tfNorm, rect.Width, rect.Height);
						tl.ParagraphAlignment = SharpDX.DirectWrite.ParagraphAlignment.Center;

						if (tl.Metrics.Width <= rect.Width)
						{
							ntlBrush.Opacity = 1.0f;
							RenderTarget.DrawTextLayout(new SharpDX.Vector2(rect.X, rect.Y), tl, ntlBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
						}

						tl.Dispose();

						if (i == ChartBars.ToIndex)
						{
							x1 = chartControl.GetXByBarIndex(ChartBars, i + 1);
							ht = tfNorm.FontSize + 4f;

							rect.X = (float)(x1 - barHalfWidth - cellWidth) - 1f;
							rect.Y = (float)(chartPanel.H - ht - offset);
							rect.Width = (float)((barHalfWidth * 2) + (cellWidth * 2)) + 1f;
							rect.Height = ht;

							tfNorm.TextAlignment = SharpDX.DirectWrite.TextAlignment.Leading;
							tfNorm.WordWrapping = SharpDX.DirectWrite.WordWrapping.NoWrap;

							TextLayout tll = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, "Delta (Cumulative)", tfNorm, rect.Width, rect.Height);
							tll.ParagraphAlignment = SharpDX.DirectWrite.ParagraphAlignment.Center;

							ntlBrush.Opacity = 0.4f;
							RenderTarget.DrawTextLayout(new SharpDX.Vector2(rect.X, rect.Y), tll, ntlBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);

							tll.Dispose();
						}
					}

					offset += (tfNorm.FontSize) + 5f;
				}

				#endregion

				#region volume text

				if (bottomTextVolume)
				{
					for (int i = ChartBars.FromIndex; i <= ChartBars.ToIndex; i++)
					{
						if (barData.BarItems.IsValidDataPointAt(i))
						{
							currBarItem = barData.BarItems.GetValueAt(i);
						}
						else
						{
							continue;
						}

						if (currBarItem == null) { continue; }
						if (currBarItem.dtc == 0) { continue; }

						x1 = chartControl.GetXByBarIndex(ChartBars, i);
						ht = tfNorm.FontSize + 4f;

						rect.X = (float)(x1 - barHalfWidth - cellWidth) - 1f;
						rect.Y = (float)(chartPanel.H - ht - offset);
						rect.Width = (float)((barHalfWidth * 2) + (cellWidth * 2)) + 1f;
						rect.Height = ht;

						bckBrush.Opacity = 1.0f;
						RenderTarget.FillRectangle(rect, bckBrush);

						opacity = (bottomAreaGradient) ? (float)Math.Max((0.4f / maxVol) * Math.Abs(currBarItem.vol), 0.05f) : 0.4f;

						if (currBarItem.cls > currBarItem.opn)
						{
							askBrush.Opacity = opacity;
							RenderTarget.FillRectangle(rect, askBrush);
						}
						else if (currBarItem.cls < currBarItem.opn)
						{
							bidBrush.Opacity = opacity;
							RenderTarget.FillRectangle(rect, bidBrush);
						}
						else
						{
							ntlBrush.Opacity = opacity;
							RenderTarget.FillRectangle(rect, ntlBrush);
						}

						tfNorm.TextAlignment = SharpDX.DirectWrite.TextAlignment.Center;
						tfNorm.WordWrapping = SharpDX.DirectWrite.WordWrapping.NoWrap;

						TextLayout tl = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, string.Format("{0:n0}", currBarItem.vol), tfNorm, rect.Width, rect.Height);
						tl.ParagraphAlignment = SharpDX.DirectWrite.ParagraphAlignment.Center;

						if (tl.Metrics.Width <= rect.Width)
						{
							ntlBrush.Opacity = 1.0f;
							RenderTarget.DrawTextLayout(new SharpDX.Vector2(rect.X, rect.Y), tl, ntlBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
						}

						tl.Dispose();

						if (i == ChartBars.ToIndex)
						{
							x1 = chartControl.GetXByBarIndex(ChartBars, i + 1);
							ht = tfNorm.FontSize + 4f;

							rect.X = (float)(x1 - barHalfWidth - cellWidth) - 1f;
							rect.Y = (float)(chartPanel.H - ht - offset);
							rect.Width = (float)((barHalfWidth * 2) + (cellWidth * 2)) + 1f;
							rect.Height = ht;

							tfNorm.TextAlignment = SharpDX.DirectWrite.TextAlignment.Leading;
							tfNorm.WordWrapping = SharpDX.DirectWrite.WordWrapping.NoWrap;

							TextLayout tll = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, "Volume", tfNorm, rect.Width, rect.Height);
							tll.ParagraphAlignment = SharpDX.DirectWrite.ParagraphAlignment.Center;

							ntlBrush.Opacity = 0.4f;
							RenderTarget.DrawTextLayout(new SharpDX.Vector2(rect.X, rect.Y), tll, ntlBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);

							tll.Dispose();
						}
					}

					offset += (tfNorm.FontSize) + 5f;
				}

				#endregion

				#region delta text

				if (bottomTextDelta)
				{
					for (int i = ChartBars.FromIndex; i <= ChartBars.ToIndex; i++)
					{
						if (barData.BarItems.IsValidDataPointAt(i))
						{
							currBarItem = barData.BarItems.GetValueAt(i);
						}
						else
						{
							continue;
						}

						if (currBarItem == null) { continue; }
						if (currBarItem.dtc == 0) { continue; }

						x1 = chartControl.GetXByBarIndex(ChartBars, i);
						ht = tfNorm.FontSize + 4f;

						rect.X = (float)(x1 - barHalfWidth - cellWidth) - 1f;
						rect.Y = (float)(chartPanel.H - ht - offset);
						rect.Width = (float)((barHalfWidth * 2) + (cellWidth * 2)) + 1f;
						rect.Height = ht;

						bckBrush.Opacity = 1.0f;
						RenderTarget.FillRectangle(rect, bckBrush);

						opacity = (bottomAreaGradient) ? (float)Math.Max((0.4f / maxDtc) * Math.Abs(currBarItem.dtc), 0.05f) : 0.4f;

						if (currBarItem.dtc > 0.0)
						{
							askBrush.Opacity = opacity;
							RenderTarget.FillRectangle(rect, askBrush);
						}
						else if (currBarItem.dtc < 0.0)
						{
							bidBrush.Opacity = opacity;
							RenderTarget.FillRectangle(rect, bidBrush);
						}
						else
						{
							ntlBrush.Opacity = opacity;
							RenderTarget.FillRectangle(rect, ntlBrush);
						}

						tfNorm.TextAlignment = SharpDX.DirectWrite.TextAlignment.Center;
						tfNorm.WordWrapping = SharpDX.DirectWrite.WordWrapping.NoWrap;

						TextLayout tl = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, string.Format("{0:n0}", Math.Abs(currBarItem.dtc)), tfNorm, rect.Width, rect.Height);
						tl.ParagraphAlignment = SharpDX.DirectWrite.ParagraphAlignment.Center;

						if (tl.Metrics.Width <= rect.Width)
						{
							ntlBrush.Opacity = 1.0f;
							RenderTarget.DrawTextLayout(new SharpDX.Vector2(rect.X, rect.Y), tl, ntlBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
						}

						tl.Dispose();

						if (i == ChartBars.ToIndex)
						{
							x1 = chartControl.GetXByBarIndex(ChartBars, i + 1);
							ht = tfNorm.FontSize + 4f;

							rect.X = (float)(x1 - barHalfWidth - cellWidth) - 1f;
							rect.Y = (float)(chartPanel.H - ht - offset);
							rect.Width = (float)((barHalfWidth * 2) + (cellWidth * 2)) + 1f;
							rect.Height = ht;

							tfNorm.TextAlignment = SharpDX.DirectWrite.TextAlignment.Leading;
							tfNorm.WordWrapping = SharpDX.DirectWrite.WordWrapping.NoWrap;

							TextLayout tll = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, "Delta", tfNorm, rect.Width, rect.Height);
							tll.ParagraphAlignment = SharpDX.DirectWrite.ParagraphAlignment.Center;

							ntlBrush.Opacity = 0.4f;
							RenderTarget.DrawTextLayout(new SharpDX.Vector2(rect.X, rect.Y), tll, ntlBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);

							tll.Dispose();
						}
					}

					offset += (tfNorm.FontSize) + 5f;
				}

				#endregion
			}

			if (showBottomArea)
			{
				if (maxPix - offset < 10f)
				{
					return;
				}

				#region cumulative delta

				if (bottomAreaType == FPV2BottomAreaType.CumulativeDelta)
				{
					List<cdArea> cdAreas = new List<cdArea>();

					for (int i = ChartBars.FromIndex; i <= ChartBars.ToIndex; i++)
					{
						if (barData.BarItems.IsValidDataPointAt(i))
						{
							currBarItem = barData.BarItems.GetValueAt(i);
						}
						else
						{
							continue;
						}

						if (currBarItem == null) { continue; }
						if (currBarItem.rowItems.IsEmpty) { continue; }

						if (cdAreas.Count == 0)
						{
							cdAreas.Add(new cdArea(i, i, currBarItem.cdh, currBarItem.cdl));

							continue;
						}

						if (currBarItem.ifb)
						{
							cdAreas.Add(new cdArea(i, i, currBarItem.cdh, currBarItem.cdl));
						}
						else
						{
							cdAreas[cdAreas.Count - 1].cdTo = i;
							cdAreas[cdAreas.Count - 1].cdHi = (currBarItem.cdh > cdAreas[cdAreas.Count - 1].cdHi) ? currBarItem.cdh : cdAreas[cdAreas.Count - 1].cdHi;
							cdAreas[cdAreas.Count - 1].cdLo = (currBarItem.cdl < cdAreas[cdAreas.Count - 1].cdLo) ? currBarItem.cdl : cdAreas[cdAreas.Count - 1].cdLo;
						}
					}

					foreach (cdArea cda in cdAreas)
					{
						double cdRng = Math.Abs(cda.cdHi - cda.cdLo);
						double cdFac = (maxPix - offset) / cdRng;

						/// panel background

						x1 = chartControl.GetXByBarIndex(ChartBars, Math.Max(cda.cdFr - 1, 0));
						x2 = chartControl.GetXByBarIndex(ChartBars, cda.cdTo);
						wt = Math.Abs(x1 - x2);
						wt = (cda.cdTo == ChartBars.ToIndex && !showFootprint) ? wt + (float)(barWid / 2) : wt;
						wt = (cda.cdTo == ChartBars.ToIndex && showFootprint) ? wt + (float)(barHalfWidth + cellWidth) : wt;
						ht = maxPix - offset;
						xl = x1 + wt + 3f;

						if (cda.cdLo >= 0.0)
						{
							rect.X = x1;
							rect.Y = (float)(chartPanel.H - ht - offset);
							rect.Width = wt;
							rect.Height = ht;

							askBrush.Opacity = 0.04f;
							RenderTarget.FillRectangle(rect, askBrush);
						}
						if (cda.cdHi <= 0.0)
						{
							rect.X = x1;
							rect.Y = (float)(chartPanel.H - ht - offset);
							rect.Width = wt;
							rect.Height = ht;

							bidBrush.Opacity = 0.04f;
							RenderTarget.FillRectangle(rect, bidBrush);
						}
						if (cda.cdLo < 0.0 && cda.cdHi > 0.0)
						{
							rect.X = x1;
							rect.Y = (float)(chartPanel.H - ((0.0 - cda.cdLo) * cdFac)) - offset;
							rect.Width = wt;
							rect.Height = (float)((0.0 - cda.cdLo) * cdFac);

							bidBrush.Opacity = 0.04f;
							RenderTarget.FillRectangle(rect, bidBrush);

							rect.X = x1;
							rect.Y = (float)(chartPanel.H - ((cda.cdHi - cda.cdLo) * cdFac) - offset);
							rect.Width = wt;
							rect.Height = (float)(maxPix - ((0.0 - cda.cdLo) * cdFac) - offset);

							askBrush.Opacity = 0.04f;
							RenderTarget.FillRectangle(rect, askBrush);

							vec1.X = x1;
							vec1.Y = (float)(chartPanel.H - ((0.0 - cda.cdLo) * cdFac) - offset);

							vec2.X = x1 + wt;
							vec2.Y = (float)(chartPanel.H - ((0.0 - cda.cdLo) * cdFac) - offset);

							ntlBrush.Opacity = 0.1f;
							RenderTarget.DrawLine(vec1, vec2, ntlBrush, 1);
						}

						/// ---

						for (int i = cda.cdFr; i <= cda.cdTo; i++)
						{
							if (barData.BarItems.IsValidDataPointAt(i))
							{
								currBarItem = barData.BarItems.GetValueAt(i);
							}
							else
							{
								continue;
							}

							if (currBarItem == null) { continue; }

							/// body

							x1 = chartControl.GetXByBarIndex(ChartBars, i);
							y1 = (float)((currBarItem.cdo - cda.cdLo) * cdFac);
							y2 = (float)((currBarItem.cdc - cda.cdLo) * cdFac);
							ht = y1 - y2;

							if (showFootprint)
							{
								rect.X = (float)(x1 - barHalfWidth - cellWidth) - 1f;
								rect.Y = (float)(chartPanel.H - y1 - offset);
								rect.Width = (float)((barHalfWidth * 2) + (cellWidth * 2)) + 1f;
								rect.Height = (ht > 0f) ? Math.Max(1f, ht) : Math.Min(-1f, ht);
							}
							else
							{
								rect.X = (float)(x1 - (barWid / 2));
								rect.Y = (float)(chartPanel.H - y1 - offset);
								rect.Width = (float)(barWid);
								rect.Height = (ht > 0f) ? Math.Max(1f, ht) : Math.Min(-1f, ht);
							}

							/// label

							if (bottomAreaLabel)
							{
								if (i == ChartBars.ToIndex)
								{
									if (currBarItem.dtc > 0.0)
									{
										if (currBarItem.cdc > 0.0)
										{
											drawLabel(string.Format("{0:n0}", Math.Abs(currBarItem.cdc)), false, xl, rect.Y + rect.Height, askBrush, 0.4f, ntlBrush, 1f);
										}
										if (currBarItem.cdc < 0.0)
										{
											drawLabel(string.Format("{0:n0}", Math.Abs(currBarItem.cdc)), false, xl, rect.Y + rect.Height, bidBrush, 0.4f, ntlBrush, 1f);
										}
										if (currBarItem.cdc == 0.0)
										{
											drawLabel(string.Format("{0:n0}", Math.Abs(currBarItem.cdc)), false, xl, rect.Y, ntlBrush, 0.4f, ntlBrush, 1f);
										}
									}

									if (currBarItem.dtc < 0.0)
									{
										if (currBarItem.cdc > 0.0)
										{
											drawLabel(string.Format("{0:n0}", Math.Abs(currBarItem.cdc)), false, xl, rect.Y + rect.Height, askBrush, 0.4f, ntlBrush, 1f);
										}
										if (currBarItem.cdc < 0.0)
										{
											drawLabel(string.Format("{0:n0}", Math.Abs(currBarItem.cdc)), false, xl, rect.Y + rect.Height, bidBrush, 0.4f, ntlBrush, 1f);
										}
										if (currBarItem.cdc == 0.0)
										{
											drawLabel(string.Format("{0:n0}", Math.Abs(currBarItem.cdc)), false, xl, rect.Y, ntlBrush, 0.4f, ntlBrush, 1f);
										}
									}

									if (currBarItem.dtc == 0.0)
									{
										if (currBarItem.cdc > 0.0)
										{
											drawLabel(string.Format("{0:n0}", Math.Abs(currBarItem.cdc)), false, xl, rect.Y, askBrush, 0.4f, ntlBrush, 1f);
										}
										if (currBarItem.cdc < 0.0)
										{
											drawLabel(string.Format("{0:n0}", Math.Abs(currBarItem.cdc)), false, xl, rect.Y, bidBrush, 0.4f, ntlBrush, 1f);
										}
										if (currBarItem.cdc == 0.0)
										{
											drawLabel(string.Format("{0:n0}", Math.Abs(currBarItem.cdc)), false, xl, rect.Y, ntlBrush, 0.4f, ntlBrush, 1f);
										}
									}
								}
							}

							bckBrush.Opacity = 1.0f;
							RenderTarget.FillRectangle(rect, bckBrush);

							if (currBarItem.dtc > 0.0)
							{
								askBrush.Opacity = 0.4f;
								RenderTarget.FillRectangle(rect, askBrush);
							}

							if (currBarItem.dtc < 0.0)
							{
								bidBrush.Opacity = 0.4f;
								RenderTarget.FillRectangle(rect, bidBrush);
							}

							if (currBarItem.dtc == 0.0)
							{
								ntlBrush.Opacity = 0.4f;
								RenderTarget.FillRectangle(rect, ntlBrush);
							}

							/// upper wick

							vec1.X = x1;
							vec1.Y = (float)(chartPanel.H - (currBarItem.cdh - cda.cdLo) * cdFac) - offset;

							vec2.X = x1;
							vec2.Y = (float)Math.Min(chartPanel.H - (currBarItem.cdo - cda.cdLo) * cdFac, chartPanel.H - (currBarItem.cdc - cda.cdLo) * cdFac) - offset;

							if (currBarItem.dtc > 0.0)
							{
								askBrush.Opacity = 0.4f;
								RenderTarget.DrawLine(vec1, vec2, askBrush, 1);
							}

							if (currBarItem.dtc < 0.0)
							{
								bidBrush.Opacity = 0.4f;
								RenderTarget.DrawLine(vec1, vec2, bidBrush, 1);
							}

							if (currBarItem.dtc == 0.0)
							{
								ntlBrush.Opacity = 0.4f;
								RenderTarget.DrawLine(vec1, vec2, ntlBrush, 1);
							}

							/// lower wick

							vec1.X = x1;
							vec1.Y = (float)Math.Max(chartPanel.H - (currBarItem.cdo - cda.cdLo) * cdFac, chartPanel.H - (currBarItem.cdc - cda.cdLo) * cdFac) - offset;

							vec2.X = x1;
							vec2.Y = (float)(chartPanel.H - (currBarItem.cdl - cda.cdLo) * cdFac) - offset;

							if (currBarItem.dtc > 0.0)
							{
								askBrush.Opacity = 0.4f;
								RenderTarget.DrawLine(vec1, vec2, askBrush, 1);
							}

							if (currBarItem.dtc < 0.0)
							{
								bidBrush.Opacity = 0.4f;
								RenderTarget.DrawLine(vec1, vec2, bidBrush, 1);
							}

							if (currBarItem.dtc == 0.0)
							{
								ntlBrush.Opacity = 0.4f;
								RenderTarget.DrawLine(vec1, vec2, ntlBrush, 1);
							}
						}
					}
				}

				#endregion

				#region delta

				if (bottomAreaType == FPV2BottomAreaType.Delta)
				{
					for (int i = ChartBars.FromIndex; i <= ChartBars.ToIndex; i++)
					{
						if (barData.BarItems.IsValidDataPointAt(i))
						{
							currBarItem = barData.BarItems.GetValueAt(i);
						}
						else
						{
							continue;
						}

						if (currBarItem == null) { continue; }
						if (currBarItem.dtc == 0) { continue; }

						x1 = chartControl.GetXByBarIndex(ChartBars, i);
						ht = (float)Math.Round(((maxPix - offset) / maxDtc) * Math.Abs(currBarItem.dtc));

						if (showFootprint)
						{
							rect.X = (float)(x1 - barHalfWidth - cellWidth) - 1f;
							rect.Y = (float)(chartPanel.H - ht - offset);
							rect.Width = (float)((barHalfWidth * 2) + (cellWidth * 2)) + 1f;
							rect.Height = ht;
						}
						else
						{
							rect.X = (float)(x1 - (barWid / 2));
							rect.Y = (float)(chartPanel.H - ht - offset);
							rect.Width = (float)(barWid);
							rect.Height = ht;
						}

						bckBrush.Opacity = 1.0f;
						RenderTarget.FillRectangle(rect, bckBrush);

						if (currBarItem.dtc > 0.0)
						{
							askBrush.Opacity = (bottomAreaGradient) ? (float)Math.Max((0.4f / maxDtc) * Math.Abs(currBarItem.dtc), 0.05f) : 0.4f;
							RenderTarget.FillRectangle(rect, askBrush);
						}

						if (currBarItem.dtc < 0.0)
						{
							bidBrush.Opacity = (bottomAreaGradient) ? (float)Math.Max((0.4f / maxDtc) * Math.Abs(currBarItem.dtc), 0.05f) : 0.4f;
							RenderTarget.FillRectangle(rect, bidBrush);
						}

						if (currBarItem.dtc == 0.0)
						{
							bidBrush.Opacity = (bottomAreaGradient) ? (float)Math.Max((0.4f / maxDtc) * Math.Abs(currBarItem.dtc), 0.05f) : 0.4f;
							RenderTarget.FillRectangle(rect, ntlBrush);
						}

						/// label

						if (bottomAreaLabel)
						{
							if (i == ChartBars.ToIndex)
							{
								float cph = (float)chartPanel.H;
								float lhh = (float)Math.Floor((ChartControl.Properties.LabelFont.Size + 4f) / 2f);

								rect.Y = (rect.Y > cph - lhh) ? cph - lhh : rect.Y;

								if (currBarItem.dtc > 0.0)
								{
									drawLabel(string.Format("{0:n0}", Math.Abs(currBarItem.dtc)), false, (float)(rect.X + rect.Width + 3f), rect.Y, askBrush, askBrush.Opacity, ntlBrush, 1f);
								}

								if (currBarItem.dtc < 0.0)
								{
									drawLabel(string.Format("{0:n0}", Math.Abs(currBarItem.dtc)), false, (float)(rect.X + rect.Width + 3f), rect.Y, bidBrush, bidBrush.Opacity, ntlBrush, 1f);
								}

								if (currBarItem.dtc == 0.0)
								{
									drawLabel(string.Format("{0:n0}", Math.Abs(currBarItem.dtc)), false, (float)(rect.X + rect.Width + 3f), rect.Y, ntlBrush, ntlBrush.Opacity, ntlBrush, 1f);
								}
							}
						}
					}
				}

				#endregion

				#region volume

				if (bottomAreaType == FPV2BottomAreaType.Volume)
				{
					for (int i = ChartBars.FromIndex; i <= ChartBars.ToIndex; i++)
					{
						if (barData.BarItems.IsValidDataPointAt(i))
						{
							currBarItem = barData.BarItems.GetValueAt(i);
						}
						else
						{
							continue;
						}

						if (currBarItem == null) { continue; }
						if (currBarItem.dtc == 0) { continue; }

						x1 = chartControl.GetXByBarIndex(ChartBars, i);
						ht = (float)Math.Round(((maxPix - offset) / maxVol) * Math.Abs(currBarItem.vol));

						if (showFootprint)
						{
							rect.X = (float)(x1 - barHalfWidth - cellWidth) - 1f;
							rect.Y = (float)(chartPanel.H - ht - offset);
							rect.Width = (float)((barHalfWidth * 2) + (cellWidth * 2)) + 1f;
							rect.Height = ht;
						}
						else
						{
							rect.X = (float)(x1 - (barWid / 2));
							rect.Y = (float)(chartPanel.H - ht - offset);
							rect.Width = (float)(barWid);
							rect.Height = ht;
						}

						bckBrush.Opacity = 1.0f;
						RenderTarget.FillRectangle(rect, bckBrush);

						if (currBarItem.cls > currBarItem.opn)
						{
							askBrush.Opacity = (bottomAreaGradient) ? (float)Math.Max((0.4f / maxVol) * Math.Abs(currBarItem.vol), 0.05f) : 0.4f;
							RenderTarget.FillRectangle(rect, askBrush);
						}
						else if (currBarItem.cls < currBarItem.opn)
						{
							bidBrush.Opacity = (bottomAreaGradient) ? (float)Math.Max((0.4f / maxVol) * Math.Abs(currBarItem.vol), 0.05f) : 0.4f;
							RenderTarget.FillRectangle(rect, bidBrush);
						}
						else
						{
							ntlBrush.Opacity = (bottomAreaGradient) ? (float)Math.Max((0.4f / maxVol) * Math.Abs(currBarItem.vol), 0.05f) : 0.4f;
							RenderTarget.FillRectangle(rect, ntlBrush);
						}

						/// label

						if (bottomAreaLabel)
						{
							if (i == ChartBars.ToIndex)
							{
								float cph = (float)chartPanel.H;
								float lhh = (float)Math.Floor((ChartControl.Properties.LabelFont.Size + 4f) / 2f);

								rect.Y = (rect.Y > cph - lhh) ? cph - lhh : rect.Y;

								if (currBarItem.cls > currBarItem.opn)
								{
									drawLabel(string.Format("{0:n0}", Math.Abs(currBarItem.vol)), false, (float)(rect.X + rect.Width + 3f), rect.Y, askBrush, askBrush.Opacity, ntlBrush, 1f);
								}

								if (currBarItem.cls < currBarItem.opn)
								{

									drawLabel(string.Format("{0:n0}", Math.Abs(currBarItem.vol)), false, (float)(rect.X + rect.Width + 3f), rect.Y, bidBrush, bidBrush.Opacity, ntlBrush, 1f);
								}

								if (currBarItem.cls == currBarItem.opn)
								{
									drawLabel(string.Format("{0:n0}", Math.Abs(currBarItem.vol)), false, (float)(rect.X + rect.Width + 3f), rect.Y, ntlBrush, ntlBrush.Opacity, ntlBrush, 1f);
								}
							}
						}
					}
				}

				#endregion
			}

			/// ---

			chartPanel = null;
			currBarItem = null;

			/// ---

			RenderTarget.AntialiasMode = oldAntialiasMode;
		}

		#endregion

		private void drawPatterns(ChartControl chartControl, ChartScale chartScale)
		{
			if (patternDetector != null)
			{
				foreach (var pattern in patternDetector.DetectedPatterns)
				{
					if (pattern.BarIndex < ChartBars.FromIndex || pattern.BarIndex > ChartBars.ToIndex)
						continue;
					float x = chartControl.GetXByBarIndex(ChartBars, pattern.BarIndex);
					float y = chartScale.GetYByValue(pattern.Price);

					switch (pattern.PatternType)
					{
						case "Absorption-Ask":
							RenderTarget.DrawEllipse(new SharpDX.Direct2D1.Ellipse(new SharpDX.Vector2(x, y), 6, 6), askBrush, 2);
							break;
						case "Absorption-Bid":
							RenderTarget.DrawEllipse(new SharpDX.Direct2D1.Ellipse(new SharpDX.Vector2(x, y), 6, 6), bidBrush, 2);
							break;
						case "ImbalanceCluster-Ask":
						case "ImbalanceCluster-Bid":
							RenderTarget.DrawRectangle(new SharpDX.RectangleF(x - 5, y - 5, 10, 10), bidBrush, 2);
							break;
						case "DeltaDivergence-High":
						case "DeltaDivergence-Low":
							RenderTarget.DrawLine(new SharpDX.Vector2(x - 7, y), new SharpDX.Vector2(x + 7, y), proBrush, 2);
							break;
						case "ZeroPrint-High":
						case "ZeroPrint-Low":
							RenderTarget.DrawEllipse(new SharpDX.Direct2D1.Ellipse(new SharpDX.Vector2(x, y), 4, 4), bidBrush, 2);
							break;
						case "InitiatingBuy-Open":
						case "InitiatingSell-Close":
							RenderTarget.DrawRectangle(new SharpDX.RectangleF(x - 4, y - 4, 8, 8), askBrush, 2);
							break;
						case "BuyingTail":
						case "SellingTail":
							RenderTarget.DrawEllipse(new SharpDX.Direct2D1.Ellipse(new SharpDX.Vector2(x, y), 5, 5), proBrush, 2);
							break;
						case "AbsAgg-Breakout":
						case "AbsAgg-Breakdown":
							RenderTarget.DrawLine(new SharpDX.Vector2(x, y - 8), new SharpDX.Vector2(x, y + 8), bidBrush, 2);
							break;
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
		[Range(0, int.MaxValue)]
		[Display(Name = "Right Margin", GroupName = "General", Order = 0)]
		public int rightMargin
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Range(0, int.MaxValue)]
		[Display(Name = "Left Margin", GroupName = "General", Order = 1)]
		public int leftMargin
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Show Close", GroupName = "General", Order = 2)]
		public bool showClose
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Paint Bars", GroupName = "General", Order = 3)]
		public bool paintBars
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Paint Bars by Delta", GroupName = "General", Order = 4)]
		public bool paintBarsByDelta
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Stacked Imbalances", GroupName = "General", Order = 5)]
		public bool showStackedImbalances
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "POC on Bar Chart", GroupName = "General", Order = 6)]
		public bool showPocOnBarChart
		{ get; set; }

		///
		/// ---------------------------------------------------------------------------------
		///

		[NinjaScriptProperty]
		[Display(Name = "Show", GroupName = "Profile (Previous Session)", Order = 0)]
		public bool prevProfileShow
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Range(0, int.MaxValue)]
		[Display(Name = "Width", GroupName = "Profile (Previous Session)", Order = 1)]
		public int prevProfileWidth
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Gradient", GroupName = "Profile (Previous Session)", Order = 2)]
		public bool prevProfileGradient
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Value Area", GroupName = "Profile (Previous Session)", Order = 3)]
		public bool prevProfileValueArea
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Extend Value Area", GroupName = "Profile (Previous Session)", Order = 4)]
		public bool prevProfileExtendVa
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Show Delta", GroupName = "Profile (Previous Session)", Order = 5)]
		public bool prevProfileShowDelta
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Show Info", GroupName = "Profile (Previous Session)", Order = 6)]
		public bool prevProfileShowInfo
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Bar", GroupName = "Profile (Previous Session)", Order = 7)]
		public bool prevProfileBar
		{ get; set; }

		///
		/// ---------------------------------------------------------------------------------
		///

		[NinjaScriptProperty]
		[Display(Name = "Show", GroupName = "Profile (Current Session)", Order = 0)]
		public bool currProfileShow
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Range(0, int.MaxValue)]
		[Display(Name = "Width", GroupName = "Profile (Current Session)", Order = 1)]
		public int currProfileWidth
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Gradient", GroupName = "Profile (Current Session)", Order = 2)]
		public bool currProfileGradient
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Value Area", GroupName = "Profile (Current Session)", Order = 3)]
		public bool currProfileValueArea
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Extend Value Area", GroupName = "Profile (Current Session)", Order = 4)]
		public bool currProfileExtendVa
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Show Delta", GroupName = "Profile (Current Session)", Order = 5)]
		public bool currProfileShowDelta
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Show Info", GroupName = "Profile (Current Session)", Order = 6)]
		public bool currProfileShowInfo
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Bar", GroupName = "Profile (Current Session)", Order = 7)]
		public bool currProfileBar
		{ get; set; }

		///
		/// ---------------------------------------------------------------------------------
		///

		[NinjaScriptProperty]
		[Display(Name = "Show", GroupName = "Profile (Custom)", Order = 0)]
		public bool custProfileShow
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Range(0, int.MaxValue)]
		[Display(Name = "Width", GroupName = "Profile (Custom)", Order = 1)]
		public int custProfileWidth
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Gradient", GroupName = "Profile (Custom)", Order = 2)]
		public bool custProfileGradient
		{ get; set; }


		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Value Area", GroupName = "Profile (Custom)", Order = 3)]
		public bool custProfileValueArea
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Extend Value Area", GroupName = "Profile (Custom)", Order = 4)]
		public bool custProfileExtendVa
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Show Delta", GroupName = "Profile (Custom)", Order = 5)]
		public bool custProfileShowDelta
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Show Info", GroupName = "Profile (Custom)", Order = 6)]
		public bool custProfileShowInfo
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Range(0.0, 100.0)]
		[Display(Name = "Percent Value", GroupName = "Profile (Custom)", Order = 7)]
		public double custProfilePctValue
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name = "Bar Value", GroupName = "Profile (Custom)", Order = 8)]
		public int custProfileBarValue
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Range(1.0, double.MaxValue)]
		[Display(Name = "Volume Value", GroupName = "Profile (Custom)", Order = 9)]
		public double custProfileVolValue
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Range(1.0, double.MaxValue)]
		[Display(Name = "Range Value (Ticks)", GroupName = "Profile (Custom)", Order = 10)]
		public double custProfileRngValue
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Map", GroupName = "Profile (Custom)", Order = 11)]
		public bool custProfileMap
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Map Display Type", GroupName = "Profile (Custom)", Order = 12)]
		public FPV2MapDisplayType custProfileMapType
		{ get; set; }

		///
		/// ---------------------------------------------------------------------------------
		///

		[NinjaScriptProperty]
		[ReadOnly(true)]
		[Display(Name = "Show", GroupName = "Footprint", Order = 0)]
		public bool showFootprint
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Display Type", GroupName = "Footprint", Order = 1)]
		public FPV2FootprintDisplayType footprintDisplayType
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Imbalances", GroupName = "Footprint", Order = 2)]
		public bool footprintImbalances
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Range(0.1, 1.0)]
		[Display(Name = "Min. Imbalance Ratio", GroupName = "Footprint", Order = 3)]
		public double minImbalanceRatio
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Delta Outline", GroupName = "Footprint", Order = 4)]
		public bool footprintDeltaOutline
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Delta Profile", GroupName = "Footprint", Order = 5)]
		public bool footprintDeltaProfile
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Delta Profile Gradient", GroupName = "Footprint", Order = 6)]
		public bool footprintDeltaGradient
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Gradient", GroupName = "Footprint", Order = 7)]
		public bool footprintGradient
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Relative Volume", GroupName = "Footprint", Order = 8)]
		public bool footprintRelativeVolume
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Volume per Bar", GroupName = "Footprint", Order = 9)]
		public bool footprintBarVolume
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Delta per Bar", GroupName = "Footprint", Order = 10)]
		public bool footprintBarDelta
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Cumulative Swing Delta", GroupName = "Footprint", Order = 11)]
		public bool footprintBarDeltaSwing
		{ get; set; }

		///
		/// ---------------------------------------------------------------------------------
		///

		[NinjaScriptProperty]
		[Display(Name = "Show", GroupName = "Bottom Area", Order = 0)]
		public bool showBottomArea
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Type", GroupName = "Bottom Area", Order = 1)]
		public FPV2BottomAreaType bottomAreaType
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Gradient", GroupName = "Bottom Area", Order = 2)]
		public bool bottomAreaGradient
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Label", GroupName = "Bottom Area", Order = 3)]
		public bool bottomAreaLabel
		{ get; set; }

		///
		/// ---------------------------------------------------------------------------------
		///

		[NinjaScriptProperty]
		[Display(Name = "Show Delta", GroupName = "Bottom Text", Order = 0)]
		public bool bottomTextDelta
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Show Volume", GroupName = "Bottom Text", Order = 1)]
		public bool bottomTextVolume
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Show Cumulative Delta", GroupName = "Bottom Text", Order = 2)]
		public bool bottomTextCumulativeDelta
		{ get; set; }

		///
		/// ---------------------------------------------------------------------------------
		///

		[NinjaScriptProperty]
		[Display(Name = "Show", GroupName = "Tape Strip", Order = 0)]
		public bool showTapeStrip
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name = "Max. Items", GroupName = "Tape Strip", Order = 1)]
		public int tapeStripMaxItems
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Range(1, double.MaxValue)]
		[Display(Name = "SizeFilter", GroupName = "Tape Strip", Order = 2)]
		public double tapeStripFilter
		{ get; set; }


		///
		/// ---------------------------------------------------------------------------------
		///

		[NinjaScriptProperty]
		[Display(Name = "Show", GroupName = "Depth Profile", Order = 0)]
		public bool showDepth
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name = "Width", GroupName = "Depth Profile", Order = 1)]
		public int depthWidth
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Gradient", GroupName = "Depth Profile", Order = 2)]
		public bool depthGradient
		{ get; set; }

		///
		/// ---------------------------------------------------------------------------------
		///

		[NinjaScriptProperty]
		[Display(Name = "Draw Midas", GroupName = "Midas", Order = 0)]
		public bool showMidas
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Range(0.0f, 1.0f)]
		[Display(Name = "Opacity", GroupName = "Midas", Order = 1)]
		public float midasOpacity
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Range(1, 7)]
		[Display(Name = "Line Width", GroupName = "Midas", Order = 2)]
		public int midasLineWidth
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Active Only", GroupName = "Midas", Order = 3)]
		public bool onlyActive
		{ get; set; }

		///
		/// ---------------------------------------------------------------------------------
		///

		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name = "Bullish Color", GroupName = "Colors", Order = 0)]
		public Brush bullishColor
		{ get; set; }

		[Browsable(false)]
		public string bullishColorSerializable
		{
			get { return Serialize.BrushToString(bullishColor); }
			set { bullishColor = Serialize.StringToBrush(value); }
		}

		/// ---

		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name = "Bearish Color", GroupName = "Colors", Order = 1)]
		public Brush bearishColor
		{ get; set; }

		[Browsable(false)]
		public string bearishColorSerializable
		{
			get { return Serialize.BrushToString(bearishColor); }
			set { bearishColor = Serialize.StringToBrush(value); }
		}

		/// ---

		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name = "Neutral Color", GroupName = "Colors", Order = 2)]
		public Brush neutralColor
		{ get; set; }

		[Browsable(false)]
		public string neutralColorSerializable
		{
			get { return Serialize.BrushToString(neutralColor); }
			set { neutralColor = Serialize.StringToBrush(value); }
		}

		/// ---

		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name = "Profile Color", GroupName = "Colors", Order = 3)]
		public Brush profileColor
		{ get; set; }

		[Browsable(false)]
		public string profileColorSerializable
		{
			get { return Serialize.BrushToString(profileColor); }
			set { profileColor = Serialize.StringToBrush(value); }
		}

		/// ---

		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name = "Map Color", GroupName = "Colors", Order = 4)]
		public Brush mapColor
		{ get; set; }

		[Browsable(false)]
		public string mapColorSerializable
		{
			get { return Serialize.BrushToString(mapColor); }
			set { mapColor = Serialize.StringToBrush(value); }
		}

		/// ---

		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name = "POC Color", GroupName = "Colors", Order = 5)]
		public Brush pocColor
		{ get; set; }

		[Browsable(false)]
		public string pocColorSerializable
		{
			get { return Serialize.BrushToString(pocColor); }
			set { pocColor = Serialize.StringToBrush(value); }
		}

		/// ---

		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name = "Stacked Imbalance Color", GroupName = "Colors", Order = 6)]
		public Brush stackedColor
		{ get; set; }

		[Browsable(false)]
		public string stackedColorSerializable
		{
			get { return Serialize.BrushToString(stackedColor); }
			set { stackedColor = Serialize.StringToBrush(value); }
		}

		/// ---

		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name = "Depth Ask Color", GroupName = "Colors", Order = 7)]
		public Brush depthAskColor
		{ get; set; }

		[Browsable(false)]
		public string depthAskColorSerializable
		{
			get { return Serialize.BrushToString(depthAskColor); }
			set { depthAskColor = Serialize.StringToBrush(value); }
		}

		/// ---

		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name = "Depth Bid Color", GroupName = "Colors", Order = 8)]
		public Brush depthBidColor
		{ get; set; }

		[Browsable(false)]
		public string depthBidColorSerializable
		{
			get { return Serialize.BrushToString(depthBidColor); }
			set { depthBidColor = Serialize.StringToBrush(value); }
		}

		///
		/// ---------------------------------------------------------------------------------
		///

		[NinjaScriptProperty]
		[Range(0.1f, 1.0f)]
		[Display(Name = "Profiles", GroupName = "Max Opacity", Order = 0)]
		public float profileMaxOpa
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Range(0.1f, 1.0f)]
		[Display(Name = "Map", GroupName = "Max Opacity", Order = 1)]
		public float mapMaxOpa
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Range(0.1f, 1.0f)]
		[Display(Name = "Footprint", GroupName = "Max Opacity", Order = 2)]
		public float footprintMaxOpa
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Range(0.1f, 1.0f)]
		[Display(Name = "Depth Profile", GroupName = "Max Opacity", Order = 3)]
		public float depthMaxOpa
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Range(0.1f, 1.0f)]
		[Display(Name = "Stacked Imbalances", GroupName = "Max Opacity", Order = 4)]
		public float stackedImbOpa
		{ get; set; }

		///
		/// ---------------------------------------------------------------------------------
		///

		[NinjaScriptProperty]
		[Display(Name = "Toggle Footprint", GroupName = "Hotkeys", Order = 0)]
		public FPV2Hotkeys footprintHotKey
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Toggle Map", GroupName = "Hotkeys", Order = 0)]
		public FPV2Hotkeys mapHotKey
		{ get; set; }

		///
		/// ---------------------------------------------------------------------------------
		///

		[NinjaScriptProperty]
		[Display(Name = "Bullish Stacked Imbalance Alert", GroupName = "Alerts", Order = 0)]
		public bool bullishStackedImbalanceAlert
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Bullish Stacked Imbalance Sound", GroupName = "Alerts", Order = 1)]
		[PropertyEditor("NinjaTrader.Gui.Tools.FilePathPicker", Filter = "Sound Files (*.wav)|*.wav", Title = "Sound")]
		public string bullishStackedImbalanceSound
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Bearish Stacked Imbalance Alert", GroupName = "Alerts", Order = 2)]
		public bool bearishStackedImbalanceAlert
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[Display(Name = "Bearish Stacked Imbalance Sound", GroupName = "Alerts", Order = 3)]
		[PropertyEditor("NinjaTrader.Gui.Tools.FilePathPicker", Filter = "Sound Files (*.wav)|*.wav", Title = "Sound")]
		public string bearishStackedImbalanceSound
		{ get; set; }

		///
		/// ---------------------------------------------------------------------------------
		///

		[NinjaScriptProperty]
		[ReadOnly(true)]
		[Display(Name = "Regular Bar Width", GroupName = "Misc", Order = 0)]
		public double rBarWidth
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[ReadOnly(true)]
		[Display(Name = "Regular Bar Distance", GroupName = "Misc", Order = 1)]
		public float rBarDistance
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[ReadOnly(true)]
		[Display(Name = "Regular Scale Fixed", GroupName = "Misc", Order = 2)]
		public bool rScaleFixed
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[ReadOnly(true)]
		[Display(Name = "Regular Scale Max", GroupName = "Misc", Order = 3)]
		public double rScaleMax
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[ReadOnly(true)]
		[Display(Name = "Regular Scale Min", GroupName = "Misc", Order = 4)]
		public double rScaleMin
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[ReadOnly(true)]
		[Display(Name = "Footprint Bar Width", GroupName = "Misc", Order = 5)]
		public double fBarWidth
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[ReadOnly(true)]
		[Display(Name = "Footprint Bar Distance", GroupName = "Misc", Order = 6)]
		public float fBarDistance
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[ReadOnly(true)]
		[Display(Name = "Footprint Scale Fixed", GroupName = "Misc", Order = 7)]
		public bool fScaleFixed
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[ReadOnly(true)]
		[Display(Name = "Footprint Scale Max", GroupName = "Misc", Order = 8)]
		public double fScaleMax
		{ get; set; }

		/// ---

		[NinjaScriptProperty]
		[ReadOnly(true)]
		[Display(Name = "Footprint Scale Min", GroupName = "Misc", Order = 9)]
		public double fScaleMin
		{ get; set; }

		#endregion
	}
}

#region FPV2 Enums

public enum FPV2MapDisplayType
{
	Volume,
	Delta
}

public enum FPV2FootprintDisplayType
{
	Numbers,
	Profile
}

public enum FPV2BottomAreaType
{
	Volume,
	Delta,
	CumulativeDelta
}

public enum FPV2Hotkeys
{
	None,
	ShiftSpace,
	CtrlSpace
}

#endregion

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private Infinity.FootPrintV2[] cacheFootPrintV2;
		public Infinity.FootPrintV2 FootPrintV2(int rightMargin, int leftMargin, bool showClose, bool paintBars, bool paintBarsByDelta, bool showStackedImbalances, bool showPocOnBarChart, bool prevProfileShow, int prevProfileWidth, bool prevProfileGradient, bool prevProfileValueArea, bool prevProfileExtendVa, bool prevProfileShowDelta, bool prevProfileShowInfo, bool prevProfileBar, bool currProfileShow, int currProfileWidth, bool currProfileGradient, bool currProfileValueArea, bool currProfileExtendVa, bool currProfileShowDelta, bool currProfileShowInfo, bool currProfileBar, bool custProfileShow, int custProfileWidth, bool custProfileGradient, bool custProfileValueArea, bool custProfileExtendVa, bool custProfileShowDelta, bool custProfileShowInfo, double custProfilePctValue, int custProfileBarValue, double custProfileVolValue, double custProfileRngValue, bool custProfileMap, FPV2MapDisplayType custProfileMapType, bool showFootprint, FPV2FootprintDisplayType footprintDisplayType, bool footprintImbalances, double minImbalanceRatio, bool footprintDeltaOutline, bool footprintDeltaProfile, bool footprintDeltaGradient, bool footprintGradient, bool footprintRelativeVolume, bool footprintBarVolume, bool footprintBarDelta, bool footprintBarDeltaSwing, bool showBottomArea, FPV2BottomAreaType bottomAreaType, bool bottomAreaGradient, bool bottomAreaLabel, bool bottomTextDelta, bool bottomTextVolume, bool bottomTextCumulativeDelta, bool showTapeStrip, int tapeStripMaxItems, double tapeStripFilter, bool showDepth, int depthWidth, bool depthGradient, bool showMidas, float midasOpacity, int midasLineWidth, bool onlyActive, Brush bullishColor, Brush bearishColor, Brush neutralColor, Brush profileColor, Brush mapColor, Brush pocColor, Brush stackedColor, Brush depthAskColor, Brush depthBidColor, float profileMaxOpa, float mapMaxOpa, float footprintMaxOpa, float depthMaxOpa, float stackedImbOpa, FPV2Hotkeys footprintHotKey, FPV2Hotkeys mapHotKey, bool bullishStackedImbalanceAlert, string bullishStackedImbalanceSound, bool bearishStackedImbalanceAlert, string bearishStackedImbalanceSound, double rBarWidth, float rBarDistance, bool rScaleFixed, double rScaleMax, double rScaleMin, double fBarWidth, float fBarDistance, bool fScaleFixed, double fScaleMax, double fScaleMin)
		{
			return FootPrintV2(Input, rightMargin, leftMargin, showClose, paintBars, paintBarsByDelta, showStackedImbalances, showPocOnBarChart, prevProfileShow, prevProfileWidth, prevProfileGradient, prevProfileValueArea, prevProfileExtendVa, prevProfileShowDelta, prevProfileShowInfo, prevProfileBar, currProfileShow, currProfileWidth, currProfileGradient, currProfileValueArea, currProfileExtendVa, currProfileShowDelta, currProfileShowInfo, currProfileBar, custProfileShow, custProfileWidth, custProfileGradient, custProfileValueArea, custProfileExtendVa, custProfileShowDelta, custProfileShowInfo, custProfilePctValue, custProfileBarValue, custProfileVolValue, custProfileRngValue, custProfileMap, custProfileMapType, showFootprint, footprintDisplayType, footprintImbalances, minImbalanceRatio, footprintDeltaOutline, footprintDeltaProfile, footprintDeltaGradient, footprintGradient, footprintRelativeVolume, footprintBarVolume, footprintBarDelta, footprintBarDeltaSwing, showBottomArea, bottomAreaType, bottomAreaGradient, bottomAreaLabel, bottomTextDelta, bottomTextVolume, bottomTextCumulativeDelta, showTapeStrip, tapeStripMaxItems, tapeStripFilter, showDepth, depthWidth, depthGradient, showMidas, midasOpacity, midasLineWidth, onlyActive, bullishColor, bearishColor, neutralColor, profileColor, mapColor, pocColor, stackedColor, depthAskColor, depthBidColor, profileMaxOpa, mapMaxOpa, footprintMaxOpa, depthMaxOpa, stackedImbOpa, footprintHotKey, mapHotKey, bullishStackedImbalanceAlert, bullishStackedImbalanceSound, bearishStackedImbalanceAlert, bearishStackedImbalanceSound, rBarWidth, rBarDistance, rScaleFixed, rScaleMax, rScaleMin, fBarWidth, fBarDistance, fScaleFixed, fScaleMax, fScaleMin);
		}

		public Infinity.FootPrintV2 FootPrintV2(ISeries<double> input, int rightMargin, int leftMargin, bool showClose, bool paintBars, bool paintBarsByDelta, bool showStackedImbalances, bool showPocOnBarChart, bool prevProfileShow, int prevProfileWidth, bool prevProfileGradient, bool prevProfileValueArea, bool prevProfileExtendVa, bool prevProfileShowDelta, bool prevProfileShowInfo, bool prevProfileBar, bool currProfileShow, int currProfileWidth, bool currProfileGradient, bool currProfileValueArea, bool currProfileExtendVa, bool currProfileShowDelta, bool currProfileShowInfo, bool currProfileBar, bool custProfileShow, int custProfileWidth, bool custProfileGradient, bool custProfileValueArea, bool custProfileExtendVa, bool custProfileShowDelta, bool custProfileShowInfo, double custProfilePctValue, int custProfileBarValue, double custProfileVolValue, double custProfileRngValue, bool custProfileMap, FPV2MapDisplayType custProfileMapType, bool showFootprint, FPV2FootprintDisplayType footprintDisplayType, bool footprintImbalances, double minImbalanceRatio, bool footprintDeltaOutline, bool footprintDeltaProfile, bool footprintDeltaGradient, bool footprintGradient, bool footprintRelativeVolume, bool footprintBarVolume, bool footprintBarDelta, bool footprintBarDeltaSwing, bool showBottomArea, FPV2BottomAreaType bottomAreaType, bool bottomAreaGradient, bool bottomAreaLabel, bool bottomTextDelta, bool bottomTextVolume, bool bottomTextCumulativeDelta, bool showTapeStrip, int tapeStripMaxItems, double tapeStripFilter, bool showDepth, int depthWidth, bool depthGradient, bool showMidas, float midasOpacity, int midasLineWidth, bool onlyActive, Brush bullishColor, Brush bearishColor, Brush neutralColor, Brush profileColor, Brush mapColor, Brush pocColor, Brush stackedColor, Brush depthAskColor, Brush depthBidColor, float profileMaxOpa, float mapMaxOpa, float footprintMaxOpa, float depthMaxOpa, float stackedImbOpa, FPV2Hotkeys footprintHotKey, FPV2Hotkeys mapHotKey, bool bullishStackedImbalanceAlert, string bullishStackedImbalanceSound, bool bearishStackedImbalanceAlert, string bearishStackedImbalanceSound, double rBarWidth, float rBarDistance, bool rScaleFixed, double rScaleMax, double rScaleMin, double fBarWidth, float fBarDistance, bool fScaleFixed, double fScaleMax, double fScaleMin)
		{
			if (cacheFootPrintV2 != null)
				for (int idx = 0; idx < cacheFootPrintV2.Length; idx++)
					if (cacheFootPrintV2[idx] != null && cacheFootPrintV2[idx].rightMargin == rightMargin && cacheFootPrintV2[idx].leftMargin == leftMargin && cacheFootPrintV2[idx].showClose == showClose && cacheFootPrintV2[idx].paintBars == paintBars && cacheFootPrintV2[idx].paintBarsByDelta == paintBarsByDelta && cacheFootPrintV2[idx].showStackedImbalances == showStackedImbalances && cacheFootPrintV2[idx].showPocOnBarChart == showPocOnBarChart && cacheFootPrintV2[idx].prevProfileShow == prevProfileShow && cacheFootPrintV2[idx].prevProfileWidth == prevProfileWidth && cacheFootPrintV2[idx].prevProfileGradient == prevProfileGradient && cacheFootPrintV2[idx].prevProfileValueArea == prevProfileValueArea && cacheFootPrintV2[idx].prevProfileExtendVa == prevProfileExtendVa && cacheFootPrintV2[idx].prevProfileShowDelta == prevProfileShowDelta && cacheFootPrintV2[idx].prevProfileShowInfo == prevProfileShowInfo && cacheFootPrintV2[idx].prevProfileBar == prevProfileBar && cacheFootPrintV2[idx].currProfileShow == currProfileShow && cacheFootPrintV2[idx].currProfileWidth == currProfileWidth && cacheFootPrintV2[idx].currProfileGradient == currProfileGradient && cacheFootPrintV2[idx].currProfileValueArea == currProfileValueArea && cacheFootPrintV2[idx].currProfileExtendVa == currProfileExtendVa && cacheFootPrintV2[idx].currProfileShowDelta == currProfileShowDelta && cacheFootPrintV2[idx].currProfileShowInfo == currProfileShowInfo && cacheFootPrintV2[idx].currProfileBar == currProfileBar && cacheFootPrintV2[idx].custProfileShow == custProfileShow && cacheFootPrintV2[idx].custProfileWidth == custProfileWidth && cacheFootPrintV2[idx].custProfileGradient == custProfileGradient && cacheFootPrintV2[idx].custProfileValueArea == custProfileValueArea && cacheFootPrintV2[idx].custProfileExtendVa == custProfileExtendVa && cacheFootPrintV2[idx].custProfileShowDelta == custProfileShowDelta && cacheFootPrintV2[idx].custProfileShowInfo == custProfileShowInfo && cacheFootPrintV2[idx].custProfilePctValue == custProfilePctValue && cacheFootPrintV2[idx].custProfileBarValue == custProfileBarValue && cacheFootPrintV2[idx].custProfileVolValue == custProfileVolValue && cacheFootPrintV2[idx].custProfileRngValue == custProfileRngValue && cacheFootPrintV2[idx].custProfileMap == custProfileMap && cacheFootPrintV2[idx].custProfileMapType == custProfileMapType && cacheFootPrintV2[idx].showFootprint == showFootprint && cacheFootPrintV2[idx].footprintDisplayType == footprintDisplayType && cacheFootPrintV2[idx].footprintImbalances == footprintImbalances && cacheFootPrintV2[idx].minImbalanceRatio == minImbalanceRatio && cacheFootPrintV2[idx].footprintDeltaOutline == footprintDeltaOutline && cacheFootPrintV2[idx].footprintDeltaProfile == footprintDeltaProfile && cacheFootPrintV2[idx].footprintDeltaGradient == footprintDeltaGradient && cacheFootPrintV2[idx].footprintGradient == footprintGradient && cacheFootPrintV2[idx].footprintRelativeVolume == footprintRelativeVolume && cacheFootPrintV2[idx].footprintBarVolume == footprintBarVolume && cacheFootPrintV2[idx].footprintBarDelta == footprintBarDelta && cacheFootPrintV2[idx].footprintBarDeltaSwing == footprintBarDeltaSwing && cacheFootPrintV2[idx].showBottomArea == showBottomArea && cacheFootPrintV2[idx].bottomAreaType == bottomAreaType && cacheFootPrintV2[idx].bottomAreaGradient == bottomAreaGradient && cacheFootPrintV2[idx].bottomAreaLabel == bottomAreaLabel && cacheFootPrintV2[idx].bottomTextDelta == bottomTextDelta && cacheFootPrintV2[idx].bottomTextVolume == bottomTextVolume && cacheFootPrintV2[idx].bottomTextCumulativeDelta == bottomTextCumulativeDelta && cacheFootPrintV2[idx].showTapeStrip == showTapeStrip && cacheFootPrintV2[idx].tapeStripMaxItems == tapeStripMaxItems && cacheFootPrintV2[idx].tapeStripFilter == tapeStripFilter && cacheFootPrintV2[idx].showDepth == showDepth && cacheFootPrintV2[idx].depthWidth == depthWidth && cacheFootPrintV2[idx].depthGradient == depthGradient && cacheFootPrintV2[idx].showMidas == showMidas && cacheFootPrintV2[idx].midasOpacity == midasOpacity && cacheFootPrintV2[idx].midasLineWidth == midasLineWidth && cacheFootPrintV2[idx].onlyActive == onlyActive && cacheFootPrintV2[idx].bullishColor == bullishColor && cacheFootPrintV2[idx].bearishColor == bearishColor && cacheFootPrintV2[idx].neutralColor == neutralColor && cacheFootPrintV2[idx].profileColor == profileColor && cacheFootPrintV2[idx].mapColor == mapColor && cacheFootPrintV2[idx].pocColor == pocColor && cacheFootPrintV2[idx].stackedColor == stackedColor && cacheFootPrintV2[idx].depthAskColor == depthAskColor && cacheFootPrintV2[idx].depthBidColor == depthBidColor && cacheFootPrintV2[idx].profileMaxOpa == profileMaxOpa && cacheFootPrintV2[idx].mapMaxOpa == mapMaxOpa && cacheFootPrintV2[idx].footprintMaxOpa == footprintMaxOpa && cacheFootPrintV2[idx].depthMaxOpa == depthMaxOpa && cacheFootPrintV2[idx].stackedImbOpa == stackedImbOpa && cacheFootPrintV2[idx].footprintHotKey == footprintHotKey && cacheFootPrintV2[idx].mapHotKey == mapHotKey && cacheFootPrintV2[idx].bullishStackedImbalanceAlert == bullishStackedImbalanceAlert && cacheFootPrintV2[idx].bullishStackedImbalanceSound == bullishStackedImbalanceSound && cacheFootPrintV2[idx].bearishStackedImbalanceAlert == bearishStackedImbalanceAlert && cacheFootPrintV2[idx].bearishStackedImbalanceSound == bearishStackedImbalanceSound && cacheFootPrintV2[idx].rBarWidth == rBarWidth && cacheFootPrintV2[idx].rBarDistance == rBarDistance && cacheFootPrintV2[idx].rScaleFixed == rScaleFixed && cacheFootPrintV2[idx].rScaleMax == rScaleMax && cacheFootPrintV2[idx].rScaleMin == rScaleMin && cacheFootPrintV2[idx].fBarWidth == fBarWidth && cacheFootPrintV2[idx].fBarDistance == fBarDistance && cacheFootPrintV2[idx].fScaleFixed == fScaleFixed && cacheFootPrintV2[idx].fScaleMax == fScaleMax && cacheFootPrintV2[idx].fScaleMin == fScaleMin && cacheFootPrintV2[idx].EqualsInput(input))
						return cacheFootPrintV2[idx];
			return CacheIndicator<Infinity.FootPrintV2>(new Infinity.FootPrintV2() { rightMargin = rightMargin, leftMargin = leftMargin, showClose = showClose, paintBars = paintBars, paintBarsByDelta = paintBarsByDelta, showStackedImbalances = showStackedImbalances, showPocOnBarChart = showPocOnBarChart, prevProfileShow = prevProfileShow, prevProfileWidth = prevProfileWidth, prevProfileGradient = prevProfileGradient, prevProfileValueArea = prevProfileValueArea, prevProfileExtendVa = prevProfileExtendVa, prevProfileShowDelta = prevProfileShowDelta, prevProfileShowInfo = prevProfileShowInfo, prevProfileBar = prevProfileBar, currProfileShow = currProfileShow, currProfileWidth = currProfileWidth, currProfileGradient = currProfileGradient, currProfileValueArea = currProfileValueArea, currProfileExtendVa = currProfileExtendVa, currProfileShowDelta = currProfileShowDelta, currProfileShowInfo = currProfileShowInfo, currProfileBar = currProfileBar, custProfileShow = custProfileShow, custProfileWidth = custProfileWidth, custProfileGradient = custProfileGradient, custProfileValueArea = custProfileValueArea, custProfileExtendVa = custProfileExtendVa, custProfileShowDelta = custProfileShowDelta, custProfileShowInfo = custProfileShowInfo, custProfilePctValue = custProfilePctValue, custProfileBarValue = custProfileBarValue, custProfileVolValue = custProfileVolValue, custProfileRngValue = custProfileRngValue, custProfileMap = custProfileMap, custProfileMapType = custProfileMapType, showFootprint = showFootprint, footprintDisplayType = footprintDisplayType, footprintImbalances = footprintImbalances, minImbalanceRatio = minImbalanceRatio, footprintDeltaOutline = footprintDeltaOutline, footprintDeltaProfile = footprintDeltaProfile, footprintDeltaGradient = footprintDeltaGradient, footprintGradient = footprintGradient, footprintRelativeVolume = footprintRelativeVolume, footprintBarVolume = footprintBarVolume, footprintBarDelta = footprintBarDelta, footprintBarDeltaSwing = footprintBarDeltaSwing, showBottomArea = showBottomArea, bottomAreaType = bottomAreaType, bottomAreaGradient = bottomAreaGradient, bottomAreaLabel = bottomAreaLabel, bottomTextDelta = bottomTextDelta, bottomTextVolume = bottomTextVolume, bottomTextCumulativeDelta = bottomTextCumulativeDelta, showTapeStrip = showTapeStrip, tapeStripMaxItems = tapeStripMaxItems, tapeStripFilter = tapeStripFilter, showDepth = showDepth, depthWidth = depthWidth, depthGradient = depthGradient, showMidas = showMidas, midasOpacity = midasOpacity, midasLineWidth = midasLineWidth, onlyActive = onlyActive, bullishColor = bullishColor, bearishColor = bearishColor, neutralColor = neutralColor, profileColor = profileColor, mapColor = mapColor, pocColor = pocColor, stackedColor = stackedColor, depthAskColor = depthAskColor, depthBidColor = depthBidColor, profileMaxOpa = profileMaxOpa, mapMaxOpa = mapMaxOpa, footprintMaxOpa = footprintMaxOpa, depthMaxOpa = depthMaxOpa, stackedImbOpa = stackedImbOpa, footprintHotKey = footprintHotKey, mapHotKey = mapHotKey, bullishStackedImbalanceAlert = bullishStackedImbalanceAlert, bullishStackedImbalanceSound = bullishStackedImbalanceSound, bearishStackedImbalanceAlert = bearishStackedImbalanceAlert, bearishStackedImbalanceSound = bearishStackedImbalanceSound, rBarWidth = rBarWidth, rBarDistance = rBarDistance, rScaleFixed = rScaleFixed, rScaleMax = rScaleMax, rScaleMin = rScaleMin, fBarWidth = fBarWidth, fBarDistance = fBarDistance, fScaleFixed = fScaleFixed, fScaleMax = fScaleMax, fScaleMin = fScaleMin }, input, ref cacheFootPrintV2);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Infinity.FootPrintV2 FootPrintV2(int rightMargin, int leftMargin, bool showClose, bool paintBars, bool paintBarsByDelta, bool showStackedImbalances, bool showPocOnBarChart, bool prevProfileShow, int prevProfileWidth, bool prevProfileGradient, bool prevProfileValueArea, bool prevProfileExtendVa, bool prevProfileShowDelta, bool prevProfileShowInfo, bool prevProfileBar, bool currProfileShow, int currProfileWidth, bool currProfileGradient, bool currProfileValueArea, bool currProfileExtendVa, bool currProfileShowDelta, bool currProfileShowInfo, bool currProfileBar, bool custProfileShow, int custProfileWidth, bool custProfileGradient, bool custProfileValueArea, bool custProfileExtendVa, bool custProfileShowDelta, bool custProfileShowInfo, double custProfilePctValue, int custProfileBarValue, double custProfileVolValue, double custProfileRngValue, bool custProfileMap, FPV2MapDisplayType custProfileMapType, bool showFootprint, FPV2FootprintDisplayType footprintDisplayType, bool footprintImbalances, double minImbalanceRatio, bool footprintDeltaOutline, bool footprintDeltaProfile, bool footprintDeltaGradient, bool footprintGradient, bool footprintRelativeVolume, bool footprintBarVolume, bool footprintBarDelta, bool footprintBarDeltaSwing, bool showBottomArea, FPV2BottomAreaType bottomAreaType, bool bottomAreaGradient, bool bottomAreaLabel, bool bottomTextDelta, bool bottomTextVolume, bool bottomTextCumulativeDelta, bool showTapeStrip, int tapeStripMaxItems, double tapeStripFilter, bool showDepth, int depthWidth, bool depthGradient, bool showMidas, float midasOpacity, int midasLineWidth, bool onlyActive, Brush bullishColor, Brush bearishColor, Brush neutralColor, Brush profileColor, Brush mapColor, Brush pocColor, Brush stackedColor, Brush depthAskColor, Brush depthBidColor, float profileMaxOpa, float mapMaxOpa, float footprintMaxOpa, float depthMaxOpa, float stackedImbOpa, FPV2Hotkeys footprintHotKey, FPV2Hotkeys mapHotKey, bool bullishStackedImbalanceAlert, string bullishStackedImbalanceSound, bool bearishStackedImbalanceAlert, string bearishStackedImbalanceSound, double rBarWidth, float rBarDistance, bool rScaleFixed, double rScaleMax, double rScaleMin, double fBarWidth, float fBarDistance, bool fScaleFixed, double fScaleMax, double fScaleMin)
		{
			return indicator.FootPrintV2(Input, rightMargin, leftMargin, showClose, paintBars, paintBarsByDelta, showStackedImbalances, showPocOnBarChart, prevProfileShow, prevProfileWidth, prevProfileGradient, prevProfileValueArea, prevProfileExtendVa, prevProfileShowDelta, prevProfileShowInfo, prevProfileBar, currProfileShow, currProfileWidth, currProfileGradient, currProfileValueArea, currProfileExtendVa, currProfileShowDelta, currProfileShowInfo, currProfileBar, custProfileShow, custProfileWidth, custProfileGradient, custProfileValueArea, custProfileExtendVa, custProfileShowDelta, custProfileShowInfo, custProfilePctValue, custProfileBarValue, custProfileVolValue, custProfileRngValue, custProfileMap, custProfileMapType, showFootprint, footprintDisplayType, footprintImbalances, minImbalanceRatio, footprintDeltaOutline, footprintDeltaProfile, footprintDeltaGradient, footprintGradient, footprintRelativeVolume, footprintBarVolume, footprintBarDelta, footprintBarDeltaSwing, showBottomArea, bottomAreaType, bottomAreaGradient, bottomAreaLabel, bottomTextDelta, bottomTextVolume, bottomTextCumulativeDelta, showTapeStrip, tapeStripMaxItems, tapeStripFilter, showDepth, depthWidth, depthGradient, showMidas, midasOpacity, midasLineWidth, onlyActive, bullishColor, bearishColor, neutralColor, profileColor, mapColor, pocColor, stackedColor, depthAskColor, depthBidColor, profileMaxOpa, mapMaxOpa, footprintMaxOpa, depthMaxOpa, stackedImbOpa, footprintHotKey, mapHotKey, bullishStackedImbalanceAlert, bullishStackedImbalanceSound, bearishStackedImbalanceAlert, bearishStackedImbalanceSound, rBarWidth, rBarDistance, rScaleFixed, rScaleMax, rScaleMin, fBarWidth, fBarDistance, fScaleFixed, fScaleMax, fScaleMin);
		}

		public Indicators.Infinity.FootPrintV2 FootPrintV2(ISeries<double> input, int rightMargin, int leftMargin, bool showClose, bool paintBars, bool paintBarsByDelta, bool showStackedImbalances, bool showPocOnBarChart, bool prevProfileShow, int prevProfileWidth, bool prevProfileGradient, bool prevProfileValueArea, bool prevProfileExtendVa, bool prevProfileShowDelta, bool prevProfileShowInfo, bool prevProfileBar, bool currProfileShow, int currProfileWidth, bool currProfileGradient, bool currProfileValueArea, bool currProfileExtendVa, bool currProfileShowDelta, bool currProfileShowInfo, bool currProfileBar, bool custProfileShow, int custProfileWidth, bool custProfileGradient, bool custProfileValueArea, bool custProfileExtendVa, bool custProfileShowDelta, bool custProfileShowInfo, double custProfilePctValue, int custProfileBarValue, double custProfileVolValue, double custProfileRngValue, bool custProfileMap, FPV2MapDisplayType custProfileMapType, bool showFootprint, FPV2FootprintDisplayType footprintDisplayType, bool footprintImbalances, double minImbalanceRatio, bool footprintDeltaOutline, bool footprintDeltaProfile, bool footprintDeltaGradient, bool footprintGradient, bool footprintRelativeVolume, bool footprintBarVolume, bool footprintBarDelta, bool footprintBarDeltaSwing, bool showBottomArea, FPV2BottomAreaType bottomAreaType, bool bottomAreaGradient, bool bottomAreaLabel, bool bottomTextDelta, bool bottomTextVolume, bool bottomTextCumulativeDelta, bool showTapeStrip, int tapeStripMaxItems, double tapeStripFilter, bool showDepth, int depthWidth, bool depthGradient, bool showMidas, float midasOpacity, int midasLineWidth, bool onlyActive, Brush bullishColor, Brush bearishColor, Brush neutralColor, Brush profileColor, Brush mapColor, Brush pocColor, Brush stackedColor, Brush depthAskColor, Brush depthBidColor, float profileMaxOpa, float mapMaxOpa, float footprintMaxOpa, float depthMaxOpa, float stackedImbOpa, FPV2Hotkeys footprintHotKey, FPV2Hotkeys mapHotKey, bool bullishStackedImbalanceAlert, string bullishStackedImbalanceSound, bool bearishStackedImbalanceAlert, string bearishStackedImbalanceSound, double rBarWidth, float rBarDistance, bool rScaleFixed, double rScaleMax, double rScaleMin, double fBarWidth, float fBarDistance, bool fScaleFixed, double fScaleMax, double fScaleMin)
		{
			return indicator.FootPrintV2(input, rightMargin, leftMargin, showClose, paintBars, paintBarsByDelta, showStackedImbalances, showPocOnBarChart, prevProfileShow, prevProfileWidth, prevProfileGradient, prevProfileValueArea, prevProfileExtendVa, prevProfileShowDelta, prevProfileShowInfo, prevProfileBar, currProfileShow, currProfileWidth, currProfileGradient, currProfileValueArea, currProfileExtendVa, currProfileShowDelta, currProfileShowInfo, currProfileBar, custProfileShow, custProfileWidth, custProfileGradient, custProfileValueArea, custProfileExtendVa, custProfileShowDelta, custProfileShowInfo, custProfilePctValue, custProfileBarValue, custProfileVolValue, custProfileRngValue, custProfileMap, custProfileMapType, showFootprint, footprintDisplayType, footprintImbalances, minImbalanceRatio, footprintDeltaOutline, footprintDeltaProfile, footprintDeltaGradient, footprintGradient, footprintRelativeVolume, footprintBarVolume, footprintBarDelta, footprintBarDeltaSwing, showBottomArea, bottomAreaType, bottomAreaGradient, bottomAreaLabel, bottomTextDelta, bottomTextVolume, bottomTextCumulativeDelta, showTapeStrip, tapeStripMaxItems, tapeStripFilter, showDepth, depthWidth, depthGradient, showMidas, midasOpacity, midasLineWidth, onlyActive, bullishColor, bearishColor, neutralColor, profileColor, mapColor, pocColor, stackedColor, depthAskColor, depthBidColor, profileMaxOpa, mapMaxOpa, footprintMaxOpa, depthMaxOpa, stackedImbOpa, footprintHotKey, mapHotKey, bullishStackedImbalanceAlert, bullishStackedImbalanceSound, bearishStackedImbalanceAlert, bearishStackedImbalanceSound, rBarWidth, rBarDistance, rScaleFixed, rScaleMax, rScaleMin, fBarWidth, fBarDistance, fScaleFixed, fScaleMax, fScaleMin);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Infinity.FootPrintV2 FootPrintV2(int rightMargin, int leftMargin, bool showClose, bool paintBars, bool paintBarsByDelta, bool showStackedImbalances, bool showPocOnBarChart, bool prevProfileShow, int prevProfileWidth, bool prevProfileGradient, bool prevProfileValueArea, bool prevProfileExtendVa, bool prevProfileShowDelta, bool prevProfileShowInfo, bool prevProfileBar, bool currProfileShow, int currProfileWidth, bool currProfileGradient, bool currProfileValueArea, bool currProfileExtendVa, bool currProfileShowDelta, bool currProfileShowInfo, bool currProfileBar, bool custProfileShow, int custProfileWidth, bool custProfileGradient, bool custProfileValueArea, bool custProfileExtendVa, bool custProfileShowDelta, bool custProfileShowInfo, double custProfilePctValue, int custProfileBarValue, double custProfileVolValue, double custProfileRngValue, bool custProfileMap, FPV2MapDisplayType custProfileMapType, bool showFootprint, FPV2FootprintDisplayType footprintDisplayType, bool footprintImbalances, double minImbalanceRatio, bool footprintDeltaOutline, bool footprintDeltaProfile, bool footprintDeltaGradient, bool footprintGradient, bool footprintRelativeVolume, bool footprintBarVolume, bool footprintBarDelta, bool footprintBarDeltaSwing, bool showBottomArea, FPV2BottomAreaType bottomAreaType, bool bottomAreaGradient, bool bottomAreaLabel, bool bottomTextDelta, bool bottomTextVolume, bool bottomTextCumulativeDelta, bool showTapeStrip, int tapeStripMaxItems, double tapeStripFilter, bool showDepth, int depthWidth, bool depthGradient, bool showMidas, float midasOpacity, int midasLineWidth, bool onlyActive, Brush bullishColor, Brush bearishColor, Brush neutralColor, Brush profileColor, Brush mapColor, Brush pocColor, Brush stackedColor, Brush depthAskColor, Brush depthBidColor, float profileMaxOpa, float mapMaxOpa, float footprintMaxOpa, float depthMaxOpa, float stackedImbOpa, FPV2Hotkeys footprintHotKey, FPV2Hotkeys mapHotKey, bool bullishStackedImbalanceAlert, string bullishStackedImbalanceSound, bool bearishStackedImbalanceAlert, string bearishStackedImbalanceSound, double rBarWidth, float rBarDistance, bool rScaleFixed, double rScaleMax, double rScaleMin, double fBarWidth, float fBarDistance, bool fScaleFixed, double fScaleMax, double fScaleMin)
		{
			return indicator.FootPrintV2(Input, rightMargin, leftMargin, showClose, paintBars, paintBarsByDelta, showStackedImbalances, showPocOnBarChart, prevProfileShow, prevProfileWidth, prevProfileGradient, prevProfileValueArea, prevProfileExtendVa, prevProfileShowDelta, prevProfileShowInfo, prevProfileBar, currProfileShow, currProfileWidth, currProfileGradient, currProfileValueArea, currProfileExtendVa, currProfileShowDelta, currProfileShowInfo, currProfileBar, custProfileShow, custProfileWidth, custProfileGradient, custProfileValueArea, custProfileExtendVa, custProfileShowDelta, custProfileShowInfo, custProfilePctValue, custProfileBarValue, custProfileVolValue, custProfileRngValue, custProfileMap, custProfileMapType, showFootprint, footprintDisplayType, footprintImbalances, minImbalanceRatio, footprintDeltaOutline, footprintDeltaProfile, footprintDeltaGradient, footprintGradient, footprintRelativeVolume, footprintBarVolume, footprintBarDelta, footprintBarDeltaSwing, showBottomArea, bottomAreaType, bottomAreaGradient, bottomAreaLabel, bottomTextDelta, bottomTextVolume, bottomTextCumulativeDelta, showTapeStrip, tapeStripMaxItems, tapeStripFilter, showDepth, depthWidth, depthGradient, showMidas, midasOpacity, midasLineWidth, onlyActive, bullishColor, bearishColor, neutralColor, profileColor, mapColor, pocColor, stackedColor, depthAskColor, depthBidColor, profileMaxOpa, mapMaxOpa, footprintMaxOpa, depthMaxOpa, stackedImbOpa, footprintHotKey, mapHotKey, bullishStackedImbalanceAlert, bullishStackedImbalanceSound, bearishStackedImbalanceAlert, bearishStackedImbalanceSound, rBarWidth, rBarDistance, rScaleFixed, rScaleMax, rScaleMin, fBarWidth, fBarDistance, fScaleFixed, fScaleMax, fScaleMin);
		}

		public Indicators.Infinity.FootPrintV2 FootPrintV2(ISeries<double> input, int rightMargin, int leftMargin, bool showClose, bool paintBars, bool paintBarsByDelta, bool showStackedImbalances, bool showPocOnBarChart, bool prevProfileShow, int prevProfileWidth, bool prevProfileGradient, bool prevProfileValueArea, bool prevProfileExtendVa, bool prevProfileShowDelta, bool prevProfileShowInfo, bool prevProfileBar, bool currProfileShow, int currProfileWidth, bool currProfileGradient, bool currProfileValueArea, bool currProfileExtendVa, bool currProfileShowDelta, bool currProfileShowInfo, bool currProfileBar, bool custProfileShow, int custProfileWidth, bool custProfileGradient, bool custProfileValueArea, bool custProfileExtendVa, bool custProfileShowDelta, bool custProfileShowInfo, double custProfilePctValue, int custProfileBarValue, double custProfileVolValue, double custProfileRngValue, bool custProfileMap, FPV2MapDisplayType custProfileMapType, bool showFootprint, FPV2FootprintDisplayType footprintDisplayType, bool footprintImbalances, double minImbalanceRatio, bool footprintDeltaOutline, bool footprintDeltaProfile, bool footprintDeltaGradient, bool footprintGradient, bool footprintRelativeVolume, bool footprintBarVolume, bool footprintBarDelta, bool footprintBarDeltaSwing, bool showBottomArea, FPV2BottomAreaType bottomAreaType, bool bottomAreaGradient, bool bottomAreaLabel, bool bottomTextDelta, bool bottomTextVolume, bool bottomTextCumulativeDelta, bool showTapeStrip, int tapeStripMaxItems, double tapeStripFilter, bool showDepth, int depthWidth, bool depthGradient, bool showMidas, float midasOpacity, int midasLineWidth, bool onlyActive, Brush bullishColor, Brush bearishColor, Brush neutralColor, Brush profileColor, Brush mapColor, Brush pocColor, Brush stackedColor, Brush depthAskColor, Brush depthBidColor, float profileMaxOpa, float mapMaxOpa, float footprintMaxOpa, float depthMaxOpa, float stackedImbOpa, FPV2Hotkeys footprintHotKey, FPV2Hotkeys mapHotKey, bool bullishStackedImbalanceAlert, string bullishStackedImbalanceSound, bool bearishStackedImbalanceAlert, string bearishStackedImbalanceSound, double rBarWidth, float rBarDistance, bool rScaleFixed, double rScaleMax, double rScaleMin, double fBarWidth, float fBarDistance, bool fScaleFixed, double fScaleMax, double fScaleMin)
		{
			return indicator.FootPrintV2(input, rightMargin, leftMargin, showClose, paintBars, paintBarsByDelta, showStackedImbalances, showPocOnBarChart, prevProfileShow, prevProfileWidth, prevProfileGradient, prevProfileValueArea, prevProfileExtendVa, prevProfileShowDelta, prevProfileShowInfo, prevProfileBar, currProfileShow, currProfileWidth, currProfileGradient, currProfileValueArea, currProfileExtendVa, currProfileShowDelta, currProfileShowInfo, currProfileBar, custProfileShow, custProfileWidth, custProfileGradient, custProfileValueArea, custProfileExtendVa, custProfileShowDelta, custProfileShowInfo, custProfilePctValue, custProfileBarValue, custProfileVolValue, custProfileRngValue, custProfileMap, custProfileMapType, showFootprint, footprintDisplayType, footprintImbalances, minImbalanceRatio, footprintDeltaOutline, footprintDeltaProfile, footprintDeltaGradient, footprintGradient, footprintRelativeVolume, footprintBarVolume, footprintBarDelta, footprintBarDeltaSwing, showBottomArea, bottomAreaType, bottomAreaGradient, bottomAreaLabel, bottomTextDelta, bottomTextVolume, bottomTextCumulativeDelta, showTapeStrip, tapeStripMaxItems, tapeStripFilter, showDepth, depthWidth, depthGradient, showMidas, midasOpacity, midasLineWidth, onlyActive, bullishColor, bearishColor, neutralColor, profileColor, mapColor, pocColor, stackedColor, depthAskColor, depthBidColor, profileMaxOpa, mapMaxOpa, footprintMaxOpa, depthMaxOpa, stackedImbOpa, footprintHotKey, mapHotKey, bullishStackedImbalanceAlert, bullishStackedImbalanceSound, bearishStackedImbalanceAlert, bearishStackedImbalanceSound, rBarWidth, rBarDistance, rScaleFixed, rScaleMax, rScaleMin, fBarWidth, fBarDistance, fScaleFixed, fScaleMax, fScaleMin);
		}
	}
}

#endregion
