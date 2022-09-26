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
	#region Globals
	
	public static class FPV2Globals
	{
	    public static double tsv = 0.0;
	}
	
	#endregion
	
	#region CategoryOrder
	
	[Gui.CategoryOrder("General", 1)]
	[Gui.CategoryOrder("Profile (Previous Session)", 2)]
	[Gui.CategoryOrder("Profile (Current Session)", 3)]
	[Gui.CategoryOrder("Profile (Custom)", 4)]
	[Gui.CategoryOrder("Footprint", 5)]
	[Gui.CategoryOrder("Bottom Area", 6)]
	[Gui.CategoryOrder("Tape Strip", 7)]
	[Gui.CategoryOrder("Colors", 8)]
	[Gui.CategoryOrder("Max Opacity", 9)]
	[Gui.CategoryOrder("Hotkeys", 10)]
	[Gui.CategoryOrder("Misc", 11)]
	
	#endregion
	
	public class FootPrintV2 : Indicator
	{
		#region RowItem
		
		public class RowItem
		{
			public double vol = 0.0;
			public double ask = 0.0;
			public double bid = 0.0;
			public double dta = 0.0;
			
			public RowItem()
			{}
			
			public RowItem(double vol, double ask, double bid)
			{
				this.vol = vol;
				this.ask = ask;
				this.bid = bid;
				
				this.dta = this.ask - this.bid;
			}
			
			public void addAsk(double vol)
			{
				this.ask += vol;
				this.vol += vol;
				
				this.dta = this.ask - this.bid;
			}
			
			public void addBid(double vol)
			{
				this.bid += vol;
				this.vol += vol;
				
				this.dta = this.ask - this.bid;
			}
			
			public void addVol(double vol)
			{
				this.vol += vol;
			}
		}
		
		#endregion
		
		#region BarItem
		
		public class BarItem
		{
			public int    idx = 0;
			public bool   ifb = false;
			public bool   clc = true;
			public double min = double.MaxValue;
			public double max = double.MinValue;
			public double rng = 0.0;
			public double opn = 0.0;
			public double cls = 0.0;
			public double vol = 0.0;
			public double ask = 0.0;
			public double bid = 0.0;
			public double dtc = 0.0;
			public double dtl = 0.0;
			public double dth = 0.0;
			public double cdo = 0.0;
			public double cdl = 0.0;
			public double cdh = 0.0;
			public double cdc = 0.0;
			public double poc = 0.0;
			public double avg = 0.0;
			
			public ConcurrentDictionary<double, RowItem> rowItems = new ConcurrentDictionary<double, RowItem>();
			
			public Profile custProfile = new Profile();
			public Profile currProfile = new Profile();
			public Profile prevProfile = new Profile();
			
			public BarItem(int idx, bool ifb)
			{
				this.idx = idx;
				this.ifb = ifb;
				
				if(ifb)
				{
					cdo = 0.0;
				}
			}
			
			/// ---
			
			public void addAsk(double prc, double vol, bool showPrevProfile, bool showCurrProfile, bool showCustProfile)
			{
				this.vol += vol;
				this.ask += vol;
				this.dtc  = this.ask - this.bid;
				this.dtl  = (this.dtc < this.dtl) ? dtc : dtl;
				this.dth  = (this.dtc > this.dth) ? dtc : dth;
				this.cdc  = this.cdo + this.dtc;
				this.cdl  = (this.cdo + this.dtl < this.cdl) ? this.cdo + this.dtl : this.cdl;
				this.cdh  = (this.cdo + this.dth > this.cdh) ? this.cdo + this.dth : this.cdh;
				this.min  = (prc < this.min) ? prc : this.min;
				this.max  = (prc > this.max) ? prc : this.max;
				this.rng  = this.max - this.min;
				this.cls  = prc;
				
				this.rowItems.GetOrAdd(prc, new RowItem());
				this.rowItems[prc].addAsk(vol);
				
				if(showCustProfile)
				{
					this.custProfile.addAsk(prc, vol);
				}
				if(showCurrProfile || showPrevProfile)
				{
					this.currProfile.addAsk(prc, vol);
				}
			}
			
			public void addBid(double prc, double vol, bool showPrevProfile, bool showCurrProfile, bool showCustProfile)
			{
				this.vol += vol;
				this.bid += vol;
				this.dtc  = this.ask - this.bid;
				this.dtl  = (this.dtc < this.dtl) ? dtc : dtl;
				this.dth  = (this.dtc > this.dth) ? dtc : dth;
				this.cdc  = this.cdo + this.dtc;
				this.cdl  = (this.cdo + this.dtl < this.cdl) ? this.cdo + this.dtl : this.cdl;
				this.cdh  = (this.cdo + this.dth > this.cdh) ? this.cdo + this.dth : this.cdh;
				this.min  = (prc < this.min) ? prc : this.min;
				this.max  = (prc > this.max) ? prc : this.max;
				this.rng  = this.max - this.min;
				this.cls  = prc;
				
				this.rowItems.GetOrAdd(prc, new RowItem());
				this.rowItems[prc].addBid(vol);
				
				if(showCustProfile)
				{
					this.custProfile.addBid(prc, vol);
				}
				if(showCurrProfile || showPrevProfile)
				{
					this.currProfile.addBid(prc, vol);
				}
			}
			
			public void addVol(double prc, double vol, bool showPrevProfile, bool showCurrProfile, bool showCustProfile)
			{
				this.vol += vol;
				this.min  = (prc < this.min) ? prc : this.min;
				this.max  = (prc > this.max) ? prc : this.max;
				this.rng  = this.max - this.min;
				this.cls  = prc;
				
				this.rowItems.GetOrAdd(prc, new RowItem());
				this.rowItems[prc].addVol(vol);
				
				if(showCustProfile)
				{
					this.custProfile.addVol(prc, vol);
				}
				if(showCurrProfile || showPrevProfile)
				{
					this.currProfile.addVol(prc, vol);
				}
			}
			
			public void calc()
			{
				this.setAvg();
				this.setPoc();
			}
			
			public void setAvg()
			{
				if(!this.rowItems.IsEmpty)
				{
					this.avg = this.vol / this.rowItems.Count;
				}
			}
			
			public void setPoc()
			{
				if(!this.rowItems.IsEmpty)
				{
					this.poc = this.rowItems.Keys.Aggregate((i, j) => this.rowItems[i].vol > this.rowItems[j].vol ? i : j);
				}
			}
			
			public double getMaxVol()
			{
				double mv = 0.0;
				
				if(!this.rowItems.IsEmpty)
				{
					foreach(KeyValuePair<double, RowItem> ri in this.rowItems)
					{
						mv = (ri.Value.ask > mv) ? ri.Value.ask : mv;
						mv = (ri.Value.bid > mv) ? ri.Value.bid : mv;
					}
				}
				
				return mv;
			}
			
			public double getMaxDta()
			{
				double md = 0.0;
				
				if(!this.rowItems.IsEmpty)
				{
					foreach(KeyValuePair<double, RowItem> ri in this.rowItems)
					{
						md = (Math.Abs(ri.Value.dta) > md) ? Math.Abs(ri.Value.dta) : md;
					}
				}
				
				return md;
			}
			
			public bool isAskImbalance(double askPrc, double bidPrc, double minImbalanceRatio)
			{
				double minVol = this.avg / 4.0;
				double askVol = (this.rowItems.ContainsKey(askPrc)) ? this.rowItems[askPrc].ask : 0.0;
				double bidVol = (this.rowItems.ContainsKey(bidPrc)) ? this.rowItems[bidPrc].bid : 0.0;
				double imbRat = (askVol - bidVol) / (askVol + bidVol);
				
				return (askPrc > this.min && askVol >= minVol && imbRat >= minImbalanceRatio);
			}
			
			public bool isBidImbalance(double askPrc, double bidPrc, double minImbalanceRatio)
			{
				double minVol = this.avg / 4.0;
				double askVol = (this.rowItems.ContainsKey(askPrc)) ? this.rowItems[askPrc].ask : 0.0;
				double bidVol = (this.rowItems.ContainsKey(bidPrc)) ? this.rowItems[bidPrc].bid : 0.0;
				double imbRat = (bidVol - askVol) / (bidVol + askVol);
				
				return (bidPrc < this.max && bidVol >= minVol && imbRat >= minImbalanceRatio);
			}
		}
		
		#endregion
		
		#region Profile
		
		public class Profile
		{
			public int    bar = 0;
			public double min = double.MaxValue;
			public double max = double.MinValue;
			public double rng = 0.0;
			public double opn = 0.0;
			public double cls = 0.0;
			public double vol = 0.0;
			public double ask = 0.0;
			public double bid = 0.0;
			public double dta = 0.0;
			public double poc = 0.0;
			public double vah = 0.0;
			public double val = 0.0;
			public double avg = 0.0;
			
			public ConcurrentDictionary<double, RowItem> rowItems = new ConcurrentDictionary<double, RowItem>();
			
			/// ---
			
			public void addAsk(double prc, double vol)
			{
				this.vol += vol;
				this.ask += vol;
				this.dta  = this.ask - this.bid;
				this.min  = (prc < this.min) ? prc : this.min;
				this.max  = (prc > this.max) ? prc : this.max;
				this.rng  = this.max - this.min;
				this.cls  = prc;
				
				this.rowItems.GetOrAdd(prc, new RowItem());
				this.rowItems[prc].addAsk(vol);
			}
			
			public void addBid(double prc, double vol)
			{
				this.vol += vol;
				this.bid += vol;
				this.dta  = this.ask - this.bid;
				this.min  = (prc < this.min) ? prc : this.min;
				this.max  = (prc > this.max) ? prc : this.max;
				this.rng  = this.max - this.min;
				this.cls  = prc;
				
				this.rowItems.GetOrAdd(prc, new RowItem());
				this.rowItems[prc].addBid(vol);
			}
			
			public void addVol(double prc, double vol)
			{
				this.vol += vol;
				this.min  = (prc < this.min) ? prc : this.min;
				this.max  = (prc > this.max) ? prc : this.max;
				this.rng  = this.max - this.min;
				this.cls  = prc;
				
				this.rowItems.GetOrAdd(prc, new RowItem());
				this.rowItems[prc].addVol(vol);
			}
			
			public void calc()
			{
				this.setAvg();
				this.setPoc();
				this.setValueArea();
			}
			
			public void setAvg()
			{
				if(!this.rowItems.IsEmpty)
				{
					this.avg = this.vol / this.rowItems.Count;
				}
			}
			
			public void setPoc()
			{
				if(!this.rowItems.IsEmpty)
				{
					this.poc = this.rowItems.Keys.Aggregate((i, j) => this.rowItems[i].vol > this.rowItems[j].vol ? i : j);
				}
			}
			
			public void setValueArea()
			{
				double vah = this.poc;
				double val = this.poc;
				
				if(!this.rowItems.IsEmpty)
				{
					int    iteCnt = 0;
					double maxPrc = this.max;
					double minPrc = this.min;
					double maxVol = this.vol * 0.682;
					double tmpVol = this.getVolume(this.poc);
					double upperP = this.poc;
					double lowerP = this.poc;
					double upperV = 0.0;
					double lowerV = 0.0;
					
					while(tmpVol < maxVol)
					{
						if((upperP >= maxPrc && lowerP <= minPrc) || iteCnt >= 1000) { break; }
						
						upperV = (upperP < maxPrc) ? getVolume(upperP + FPV2Globals.tsv) : -1.0;
						lowerV = (lowerP > minPrc) ? getVolume(lowerP - FPV2Globals.tsv) : -1.0;
						
						if(upperV > lowerV)
						{
							vah	   = upperP + FPV2Globals.tsv;
							tmpVol = tmpVol + upperV; 
							upperP = vah;
						}
						else
						{
							val	   = lowerP - FPV2Globals.tsv;
							tmpVol = tmpVol + lowerV; 
							lowerP = val;
						}
					}
				}
				
				this.vah = vah;
				this.val = val;
			}
			
			private double getVolume(double prc)
			{
				return (this.rowItems.ContainsKey(prc)) ? this.rowItems[prc].vol : 0.0;
			}
			
			public double getMaxDta()
			{
				double md = 0.0;
				
				if(!this.rowItems.IsEmpty)
				{
					foreach(KeyValuePair<double, RowItem> ri in this.rowItems)
					{
						md = (Math.Abs(ri.Value.dta) > md) ? Math.Abs(ri.Value.dta) : md;
					}
				}
				
				return md;
			}
			
			public Profile Clone()
			{
				Profile clone = new Profile();
				
				clone.bar = this.bar;
				clone.min = this.min;
				clone.max = this.max;
				clone.rng = this.rng;
				clone.opn = this.opn;
				clone.cls = this.cls;
				clone.vol = this.vol;
				clone.ask = this.ask;
				clone.bid = this.bid;
				clone.dta = this.dta;
				clone.poc = this.poc;
				clone.vah = this.vah;
				clone.val = this.val;
				clone.avg = this.avg;
				
				foreach(KeyValuePair<double, RowItem> ri in this.rowItems)
				{
					clone.rowItems.TryAdd(ri.Key, new RowItem(ri.Value.vol, ri.Value.ask, ri.Value.bid));
				}
				
				return clone;
			}
			
			public void Reset()
			{
				this.min = double.MaxValue;
				this.max = double.MinValue;
				this.rng = 0.0;
				this.opn = 0.0;
				this.cls = 0.0;
				this.vol = 0.0;
				this.ask = 0.0;
				this.bid = 0.0;
				this.dta = 0.0;
				this.poc = 0.0;
				this.vah = 0.0;
				this.val = 0.0;
				this.avg = 0.0;
				
				this.rowItems.Clear();
			}
			
			public double getDelta(double prc)
			{
				if(this.rowItems.ContainsKey(prc))
				{
					return this.rowItems[prc].ask - this.rowItems[prc].bid;
				}
				
				return 0.0;
			}
			
			public bool isAskImbalance(double askPrc, double bidPrc, double minImbalanceRatio)
			{
				double askVol = (this.rowItems.ContainsKey(askPrc)) ? this.rowItems[askPrc].ask : 0.0;
				double bidVol = (this.rowItems.ContainsKey(bidPrc)) ? this.rowItems[bidPrc].bid : 0.0;
				double imbRat = (askVol - bidVol) / (askVol + bidVol);
				
				return (askPrc > this.min && imbRat >= minImbalanceRatio);
			}
			
			public bool isBidImbalance(double askPrc, double bidPrc, double minImbalanceRatio)
			{
				double askVol = (this.rowItems.ContainsKey(askPrc)) ? this.rowItems[askPrc].ask : 0.0;
				double bidVol = (this.rowItems.ContainsKey(bidPrc)) ? this.rowItems[bidPrc].bid : 0.0;
				double imbRat = (bidVol - askVol) / (bidVol + askVol);
				
				return (bidPrc < this.max && imbRat >= minImbalanceRatio);
			}
		}
		
		#endregion
		
		#region TapeStripItem
		
		public class TapeStripItem
		{
			public int	  dir = 0;
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
		
		#region Variables
		
		private bool log = true;
		private bool bor = false;
		private bool rdy = false;
		
		private Series<BarItem> BarItems;
		
		private List<TapeStripItem> TapeStripItems = new List<TapeStripItem>();
		
		private double dTextSize 	= 0;
		private int    barFullWidth = 0;
		private int    barHalfWidth = 0;
		private int    cellWidth 	= 0;
		private bool   forceRefresh = false;
		
		SimpleFont sfMini;
		SimpleFont sfNorm;
		SimpleFont sfBold;
		
		TextFormat tfMini;
		TextFormat tfNorm;
		TextFormat tfBold;
		
		Stroke dLine = new Stroke(Brushes.Gray, DashStyleHelper.Dot, 1f);
		
		/// zz
		
		private Series<double>  ZigZagLo;
		private Series<double>  ZigZagHi;
		
		private int	   zzSpan	 = 2;
		private int    zzDir 	 = 0;
		private int    lastLoBar = 0;
		private int    lastHiBar = 0;
		private double lastLoVal = 0.0;
		private double lastHiVal = 0.0;

		/// menu
		
		private NinjaTrader.Gui.Chart.ChartTab		chartTab;
		private NinjaTrader.Gui.Chart.Chart			chartWindow;
		private bool								isToolBarButtonAdded;
		private System.Windows.DependencyObject		searchObject;
		private System.Windows.Controls.TabItem		tabItem;
		private System.Windows.Controls.Menu		theMenu;
		private string								theMenuAutomationID;
		private NinjaTrader.Gui.Tools.NTMenuItem	topMenuItem1;
		private NinjaTrader.Gui.Tools.NTMenuItem	topMenuItem2;
		private NinjaTrader.Gui.Tools.NTMenuItem	topMenuItem2SubItem1;
		private NinjaTrader.Gui.Tools.NTMenuItem	topMenuItem2SubItem2;
		private NinjaTrader.Gui.Tools.NTMenuItem	topMenuItem3;
		private NinjaTrader.Gui.Tools.NTMenuItem	topMenuItem3SubItem1;
		private NinjaTrader.Gui.Tools.NTMenuItem	topMenuItem3SubItem2;
		private NinjaTrader.Gui.Tools.NTMenuItem	topMenuItem3SubItem3;
		private NinjaTrader.Gui.Tools.NTMenuItem	topMenuItem3SubItem4;
		private NinjaTrader.Gui.Tools.NTMenuItem	topMenuItem3SubItem5;
		private NinjaTrader.Gui.Tools.NTMenuItem	topMenuItem3SubItem6;
		private NinjaTrader.Gui.Tools.NTMenuItem	topMenuItem3SubItem7;
		private NinjaTrader.Gui.Tools.NTMenuItem	topMenuItem3SubItem8;
		
		#endregion
		
		#region OnStateChange
		
		/// OnStateChange
		///
		protected override void OnStateChange()
		{
			if(State == State.SetDefaults)
			{
				Description					= @"";
				Name						= "FootPrintV2";
				Calculate					= Calculate.OnEachTick;
				IsOverlay					= true;
				DisplayInDataBox			= false;
				DrawOnPricePanel			= true;
				DrawHorizontalGridLines		= false;
				DrawVerticalGridLines		= false;
				PaintPriceMarkers			= false;
				ScaleJustification			= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				IsSuspendedWhileInactive	= false;
				
				/// ---
				
				rightMargin = 0;
				showClose	= true;
				paintBars	= true;
				
				/// ---
				
				prevProfileShow		 = true;
				prevProfileWidth 	 = 80;
				prevProfileGradient  = true;
				prevProfileBar		 = true;
				prevProfileValueArea = true;
				prevProfileExtendVa  = false;
				prevProfileShowDelta = false;
				
				/// ---
				
				currProfileShow		 = true;
				currProfileWidth 	 = 80;
				currProfileGradient  = true;
				currProfileBar		 = true;
				currProfileValueArea = true;
				currProfileExtendVa  = false;
				currProfileShowDelta = false;
				
				/// ---
				
				custProfileShow		 = true;
				custProfileWidth 	 = 80;
				custProfileGradient  = true;
				custProfileValueArea = true;
				custProfileExtendVa  = false;
				custProfileShowDelta = false;
				custProfilePctValue	 = 1;
				custProfileBarValue	 = 20;
				custProfileVolValue	 = 1;
				custProfileRngValue  = 1;
				custProfileMap		 = true;
				custProfileMapType	 = FPV2MapDisplayType.Volume;
				
				/// ---
				
				showFootprint		    = false;
				footprintDisplayType    = FPV2FootprintDisplayType.Profile;
				footprintDeltaOutline   = true;
				footprintDeltaProfile   = true;
				footprintDeltaGradient	= true;
				footprintImbalances	    = true;
				minImbalanceRatio 	    = 0.4;
				footprintGradient	    = false;
				footprintRelativeVolume = false;
				
				/// ---
				
				showBottomArea		= true;
				bottomAreaType		= FPV2BottomAreaType.Delta;
				bottomAreaGradient	= false;
				
				/// ---
				
				showTapeStrip	  = true;
				tapeStripMaxItems = 15;
				tapeStripFilter   = 10;
				
				/// ---
				
				bullishColor = Brushes.LightGreen;
				bearishColor = Brushes.LightCoral;
				neutralColor = Brushes.White;
				profileColor = Brushes.Gainsboro;
				mapColor 	 = Brushes.DodgerBlue;
				pocColor	 = Brushes.Violet;
				
				/// ---
				
				profileMaxOpa	= 0.3f;
				mapMaxOpa 		= 0.6f;
				footprintMaxOpa	= 0.4f;
				
				/// ---
				
				footprintHotKey = FPV2Hotkeys.None;
				mapHotKey 		= FPV2Hotkeys.None;
				
				/// ---
				
				rBarWidth	 = 3.0;
				rBarDistance = 9f;
				fBarWidth	 = 1.0;
				fBarDistance = 67f;
				
				/// ---
				
				AddPlot(new Stroke(Brushes.Transparent, 3f), PlotStyle.Dot, "ZigZagDots");
			}
			else if(State == State.Configure)
			{
				if(ChartBars != null)
				{
					ZOrder = ChartBars.ZOrder - 1;
				}
			}
			else if(State == State.DataLoaded)
			{
				BarItems = new Series<BarItem>(this, MaximumBarsLookBack.Infinite);
				
				FPV2Globals.tsv = TickSize;
				
				if(ChartControl != null && !isToolBarButtonAdded)
				{
					ChartControl.Dispatcher.InvokeAsync((Action)(() =>
					{
						InsertWPFControls();
					}));
				}
				
				/// ---
				
				if(ChartControl != null && ChartBars != null)
				{
					sfMini = new SimpleFont("Consolas", ChartControl.Properties.LabelFont.Size * 0.8);
					sfNorm = new SimpleFont("Consolas", ChartControl.Properties.LabelFont.Size);
					sfBold = new SimpleFont("Consolas", ChartControl.Properties.LabelFont.Size){ Bold = true };
					
					tfMini = sfMini.ToDirectWriteTextFormat();
					tfNorm = sfNorm.ToDirectWriteTextFormat();
					tfBold = sfNorm.ToDirectWriteTextFormat();
				}
				
				/// ---
				
				ZigZagLo = new Series<double>(this);
				ZigZagHi = new Series<double>(this);
				
				/// ---
				
				if(ChartControl != null)
                {
                    ChartControl.KeyDown += chartControlOnKeyDown;
                }
				
				/// ---
				
				if(log)
				{
					ClearOutputWindow();
				}
			}
			else if(State == State.Terminated)
			{
				if(ChartControl != null)
				{
					ChartControl.Dispatcher.InvokeAsync((Action)(() =>
					{
						RemoveWPFControls();
					}));
				}
				
				/// ---
				
				if(ChartControl != null)
                {
                    ChartControl.KeyDown -= chartControlOnKeyDown;
                }
				
				/// ---
				
				if(sfMini != null) sfMini = null;
				if(sfNorm != null) sfNorm = null;
				if(sfBold != null) sfBold = null;
				
				if(tfMini != null) tfMini.Dispose();
				if(tfNorm != null) tfNorm.Dispose();
				if(tfBold != null) tfBold.Dispose();
				
				TapeStripItems.Clear();
			}
		}
		
		#endregion
		
		#region chartPanelOnKeyDown
		
		public void chartControlOnKeyDown(object sender, KeyEventArgs e)
        {
			if((Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) && e.Key == Key.Space)
			{
				if(footprintHotKey == FPV2Hotkeys.ShiftSpace)
				{
					toggleFootprint();
				}
				if(custProfileShow && mapHotKey == FPV2Hotkeys.ShiftSpace)
				{
					toggleMap();
				}
			}
			else if((Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) && e.Key == Key.Space)
			{
				if(footprintHotKey == FPV2Hotkeys.CtrlSpace)
				{
					toggleFootprint();
				}
				if(custProfileShow && mapHotKey == FPV2Hotkeys.CtrlSpace)
				{
					toggleMap();
				}
			}
			else if(custProfileMap)
			{
				if(e.Key == Key.Space)
				{
					if(custProfileMapType == FPV2MapDisplayType.Volume)
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
			if(showFootprint)
			{
				showFootprint = false;
				topMenuItem3SubItem1.Header = "Show";
				
				if(footprintDisplayType == FPV2FootprintDisplayType.Profile)
				{
					fBarWidth = ChartControl.BarWidth;
					fBarDistance = ChartControl.Properties.BarDistance;
				}

				ChartControl.BarWidth = rBarWidth;
				ChartControl.Properties.BarDistance = rBarDistance;
			}
			else
			{
				showFootprint = true;
				topMenuItem3SubItem1.Header = "Hide";

				rBarWidth = ChartControl.BarWidth;
				rBarDistance = ChartControl.Properties.BarDistance;
				
				ChartControl.BarWidth = fBarWidth;
				ChartControl.Properties.BarDistance = fBarDistance;
			}
			
			topMenuItem3SubItem2.IsEnabled = showFootprint;
			topMenuItem3SubItem3.IsEnabled = showFootprint;
			topMenuItem3SubItem4.IsEnabled = showFootprint;
			topMenuItem3SubItem5.IsEnabled = showFootprint;
			topMenuItem3SubItem6.IsEnabled = showFootprint;
			topMenuItem3SubItem7.IsEnabled = showFootprint;
			topMenuItem3SubItem8.IsEnabled = showFootprint;

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
			
			foreach(System.Windows.DependencyObject item in chartWindow.MainMenu)
			{
				if(System.Windows.Automation.AutomationProperties.GetAutomationId(item) == theMenuAutomationID)
				{
					return;
				}
			}
			
			theMenu = new System.Windows.Controls.Menu
			{
				VerticalAlignment		 = VerticalAlignment.Center,
				VerticalContentAlignment = VerticalAlignment.Center,
				Style					 = System.Windows.Application.Current.TryFindResource("SystemMenuStyle") as Style
			};
			
			System.Windows.Automation.AutomationProperties.SetAutomationId(theMenu, theMenuAutomationID);
			
			System.Windows.Media.Geometry topMenuItem1Icon = System.Windows.Media.Geometry.Parse("M19.5,3.09L15,7.59V4H13V11H20V9H16.41L20.91,4.5L19.5,3.09M4,13V15H7.59L3.09,19.5L4.5,20.91L9,16.41V20H11V13H4Z");
			System.Windows.Media.Geometry topMenuItem2Icon = System.Windows.Media.Geometry.Parse("M2,2H4V20H22V22H2V2M7,10H17V13H7V10M11,15H21V18H11V15M6,4H22V8H20V6H8V8H6V4Z");
			System.Windows.Media.Geometry topMenuItem3Icon = System.Windows.Media.Geometry.Parse("M2,5H10V2H12V22H10V18H6V15H10V13H4V10H10V8H2V5M14,5H17V8H14V5M14,10H19V13H14V10M14,15H22V18H14V15Z");
			
			/// Trades
			
			topMenuItem1 = new Gui.Tools.NTMenuItem()
			{
				Header				= "Trades",
				Foreground			= (ChartControl.BarsArray[0].Properties.PlotExecutions == ChartExecutionStyle.DoNotPlot) ? Brushes.DimGray : Brushes.Silver,
				Icon				= topMenuItem1Icon,
				Margin				= new System.Windows.Thickness(0),
				Padding				= new System.Windows.Thickness(1),
				VerticalAlignment	= VerticalAlignment.Center,
				FontSize            = 11,
				Style				= System.Windows.Application.Current.TryFindResource("MainMenuItem") as Style
			};
			
			topMenuItem1.Click += topMenuItem1_Click;
			theMenu.Items.Add(topMenuItem1);
			
			/// Map
			
			topMenuItem2 = new Gui.Tools.NTMenuItem()
			{
				Header				= "Map",
				Foreground			= Brushes.Silver,
				Icon				= topMenuItem2Icon,
				Margin				= new System.Windows.Thickness(0),
				Padding				= new System.Windows.Thickness(1),
				VerticalAlignment	= VerticalAlignment.Center,
				FontSize            = 11,
				Style				= System.Windows.Application.Current.TryFindResource("MainMenuItem") as Style,
				Visibility			= (!custProfileShow) ? Visibility.Collapsed : Visibility.Visible
			};
			
			theMenu.Items.Add(topMenuItem2);
			
			topMenuItem2SubItem1 = new Gui.Tools.NTMenuItem()
			{
				Header				= (custProfileMap) ? "Hide" : "Show",
				InputGestureText 	= (mapHotKey == FPV2Hotkeys.ShiftSpace) ? "Shift+Space" : (mapHotKey == FPV2Hotkeys.CtrlSpace) ? "Ctrl+Space" : "",
				Foreground			= Brushes.Silver,
				Margin				= new System.Windows.Thickness(0),
				Padding				= new System.Windows.Thickness(1),
				VerticalAlignment	= VerticalAlignment.Center,
				FontSize            = 11,
				Style				= System.Windows.Application.Current.TryFindResource("MainMenuItem") as Style
			};
			
			topMenuItem2SubItem1.Click += topMenuItem2SubItem1_Click;
			topMenuItem2.Items.Add(topMenuItem2SubItem1);
			
			topMenuItem2SubItem2 = new Gui.Tools.NTMenuItem()
			{
				Header				= (custProfileMapType == FPV2MapDisplayType.Volume) ? "Switch to Delta" : "Switch to Volume",
				InputGestureText	= "Space",
				Foreground			= Brushes.Silver,
				Margin				= new System.Windows.Thickness(0),
				Padding				= new System.Windows.Thickness(1),
				VerticalAlignment	= VerticalAlignment.Center,
				FontSize            = 11,
				Style				= System.Windows.Application.Current.TryFindResource("MainMenuItem") as Style,
				IsEnabled			= custProfileMap
			};
			
			topMenuItem2SubItem2.Click += topMenuItem2SubItem2_Click;
			topMenuItem2.Items.Add(topMenuItem2SubItem2);
			
			/// Footprint
			
			topMenuItem3 = new Gui.Tools.NTMenuItem()
			{
				Header				= "Footprint",
				Foreground			= Brushes.Silver,
				Icon				= topMenuItem3Icon,
				Margin				= new System.Windows.Thickness(0),
				Padding				= new System.Windows.Thickness(1),
				VerticalAlignment	= VerticalAlignment.Center,
				FontSize            = 11,
				Style				= System.Windows.Application.Current.TryFindResource("MainMenuItem") as Style
			};
			
			theMenu.Items.Add(topMenuItem3);
			
			topMenuItem3SubItem1 = new Gui.Tools.NTMenuItem()
			{
				Header				= (showFootprint) ? "Hide" : "Show",
				InputGestureText 	= (footprintHotKey == FPV2Hotkeys.ShiftSpace) ? "Shift+Space" : (footprintHotKey == FPV2Hotkeys.CtrlSpace) ? "Ctrl+Space" : "",
				Foreground			= Brushes.Silver,
				Margin				= new System.Windows.Thickness(0),
				Padding				= new System.Windows.Thickness(1),
				VerticalAlignment	= VerticalAlignment.Center,
				FontSize            = 11,
				Style				= System.Windows.Application.Current.TryFindResource("MainMenuItem") as Style,
			};
			
			topMenuItem3SubItem1.Click += topMenuItem3SubItem1_Click;
			topMenuItem3.Items.Add(topMenuItem3SubItem1);
			
			topMenuItem3SubItem2 = new Gui.Tools.NTMenuItem()
			{
				Header				= (footprintDisplayType == FPV2FootprintDisplayType.Profile) ? "Switch to Numbers" : "Switch to Profile",
				Foreground			= Brushes.Silver,
				Margin				= new System.Windows.Thickness(0),
				Padding				= new System.Windows.Thickness(1),
				VerticalAlignment	= VerticalAlignment.Center,
				FontSize            = 11,
				Style				= System.Windows.Application.Current.TryFindResource("MainMenuItem") as Style,
				IsEnabled			= showFootprint
			};
			
			topMenuItem3SubItem2.Click += topMenuItem3SubItem2_Click;
			topMenuItem3.Items.Add(topMenuItem3SubItem2);
			
			topMenuItem3SubItem3 = new Gui.Tools.NTMenuItem()
			{
				Header				= "Imbalances",
				Foreground			= Brushes.Silver,
				Margin				= new System.Windows.Thickness(0),
				Padding				= new System.Windows.Thickness(1),
				VerticalAlignment	= VerticalAlignment.Center,
				FontSize            = 11,
				Style				= System.Windows.Application.Current.TryFindResource("MainMenuItem") as Style,
				IsEnabled			= showFootprint,
				IsCheckable 		= true,
				IsChecked			= footprintImbalances
			};
			
			topMenuItem3SubItem3.Click += topMenuItem3SubItem3_Click;
			topMenuItem3.Items.Add(topMenuItem3SubItem3);
			
			topMenuItem3SubItem4 = new Gui.Tools.NTMenuItem()
			{
				Header				= "Delta Outline",
				Foreground			= Brushes.Silver,
				Margin				= new System.Windows.Thickness(0),
				Padding				= new System.Windows.Thickness(1),
				VerticalAlignment	= VerticalAlignment.Center,
				FontSize            = 11,
				Style				= System.Windows.Application.Current.TryFindResource("MainMenuItem") as Style,
				IsEnabled			= showFootprint,
				IsCheckable 		= true,
				IsChecked			= footprintDeltaOutline
			};
			
			topMenuItem3SubItem4.Click += topMenuItem3SubItem4_Click;
			topMenuItem3.Items.Add(topMenuItem3SubItem4);
			
			topMenuItem3SubItem5 = new Gui.Tools.NTMenuItem()
			{
				Header				= "Gradient",
				Foreground			= Brushes.Silver,
				Margin				= new System.Windows.Thickness(0),
				Padding				= new System.Windows.Thickness(1),
				VerticalAlignment	= VerticalAlignment.Center,
				FontSize            = 11,
				Style				= System.Windows.Application.Current.TryFindResource("MainMenuItem") as Style,
				IsEnabled			= showFootprint,
				IsCheckable 		= true,
				IsChecked			= footprintGradient
			};
			
			topMenuItem3SubItem5.Click += topMenuItem3SubItem5_Click;
			topMenuItem3.Items.Add(topMenuItem3SubItem5);
			
			topMenuItem3SubItem6 = new Gui.Tools.NTMenuItem()
			{
				Header				= "Relative Volume",
				Foreground			= Brushes.Silver,
				Margin				= new System.Windows.Thickness(0),
				Padding				= new System.Windows.Thickness(1),
				VerticalAlignment	= VerticalAlignment.Center,
				FontSize            = 11,
				Style				= System.Windows.Application.Current.TryFindResource("MainMenuItem") as Style,
				IsEnabled			= showFootprint,
				IsCheckable 		= true,
				IsChecked			= footprintRelativeVolume
			};
			
			topMenuItem3SubItem6.Click += topMenuItem3SubItem6_Click;
			topMenuItem3.Items.Add(topMenuItem3SubItem6);
			
			topMenuItem3SubItem7 = new Gui.Tools.NTMenuItem()
			{
				Header				= "Delta Profile",
				Foreground			= Brushes.Silver,
				Margin				= new System.Windows.Thickness(0),
				Padding				= new System.Windows.Thickness(1),
				VerticalAlignment	= VerticalAlignment.Center,
				FontSize            = 11,
				Style				= System.Windows.Application.Current.TryFindResource("MainMenuItem") as Style,
				IsEnabled			= showFootprint,
				IsCheckable 		= true,
				IsChecked			= footprintDeltaProfile,
				Visibility			= (footprintDisplayType == FPV2FootprintDisplayType.Numbers) ? Visibility.Visible : Visibility.Collapsed
			};
			
			topMenuItem3SubItem7.Click += topMenuItem3SubItem7_Click;
			topMenuItem3.Items.Add(topMenuItem3SubItem7);
			
			topMenuItem3SubItem8 = new Gui.Tools.NTMenuItem()
			{
				Header				= "Delta Profile Gradient",
				Foreground			= Brushes.Silver,
				Margin				= new System.Windows.Thickness(0),
				Padding				= new System.Windows.Thickness(1),
				VerticalAlignment	= VerticalAlignment.Center,
				FontSize            = 11,
				Style				= System.Windows.Application.Current.TryFindResource("MainMenuItem") as Style,
				IsEnabled			= showFootprint,
				IsCheckable 		= true,
				IsChecked			= footprintDeltaGradient,
				Visibility			= (footprintDisplayType == FPV2FootprintDisplayType.Numbers) ? Visibility.Visible : Visibility.Collapsed
			};
			
			topMenuItem3SubItem8.Click += topMenuItem3SubItem8_Click;
			topMenuItem3.Items.Add(topMenuItem3SubItem8);
			
			/// ---
			
			chartWindow.MainMenu.Add(theMenu);
			
			chartWindow.MainTabControl.SelectionChanged += MySelectionChangedHandler;
		}
		
		private void MySelectionChangedHandler(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
		{
			if(e.AddedItems.Count <= 0)
			{
				return;
			}
			
			tabItem = e.AddedItems[0] as System.Windows.Controls.TabItem;
			
			if(tabItem == null)
			{
				return;
			}
			
			chartTab = tabItem.Content as NinjaTrader.Gui.Chart.ChartTab;
			
			if(chartTab != null)
			{
				if (theMenu != null)
				{
					theMenu.Visibility = (chartTab.ChartControl == ChartControl) ? Visibility.Visible : Visibility.Collapsed;
				}
			}
		}
		
		protected void RemoveWPFControls()
		{
			if(topMenuItem1 != null)
			{
				topMenuItem1.Click -= topMenuItem1_Click;
			}
			
			if(topMenuItem2SubItem1 != null)
			{
				topMenuItem2SubItem1.Click -= topMenuItem2SubItem1_Click;
			}
			
			if(topMenuItem2SubItem2 != null)
			{
				topMenuItem2SubItem2.Click -= topMenuItem2SubItem2_Click;
			}

			if(topMenuItem3SubItem1 != null)
			{
				topMenuItem3SubItem1.Click -= topMenuItem3SubItem1_Click;
			}
			
			if(topMenuItem3SubItem2 != null)
			{
				topMenuItem3SubItem2.Click -= topMenuItem3SubItem2_Click;
			}
			
			if(topMenuItem3SubItem3 != null)
			{
				topMenuItem3SubItem3.Click -= topMenuItem3SubItem3_Click;
			}
			
			if(topMenuItem3SubItem4 != null)
			{
				topMenuItem3SubItem4.Click -= topMenuItem3SubItem4_Click;
			}
			
			if(topMenuItem3SubItem5 != null)
			{
				topMenuItem3SubItem5.Click -= topMenuItem3SubItem5_Click;
			}
			
			if(topMenuItem3SubItem6 != null)
			{
				topMenuItem3SubItem6.Click -= topMenuItem3SubItem6_Click;
			}
			
			if(topMenuItem3SubItem7 != null)
			{
				topMenuItem3SubItem7.Click -= topMenuItem3SubItem7_Click;
			}
			
			if(topMenuItem3SubItem8 != null)
			{
				topMenuItem3SubItem8.Click -= topMenuItem3SubItem8_Click;
			}
			
			if(topMenuItem3SubItem8 != null)
			{
				topMenuItem3SubItem8.Click -= topMenuItem3SubItem8_Click;
			}
			
			if(theMenu != null)
			{
				chartWindow.MainMenu.Remove(theMenu);
			}
			
			chartWindow.MainTabControl.SelectionChanged -= MySelectionChangedHandler;
		}
		
		protected void topMenuItem1_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			if(ChartControl.BarsArray[0].Properties.PlotExecutions != ChartExecutionStyle.DoNotPlot)
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
			
			if(footprintDisplayType == FPV2FootprintDisplayType.Numbers)
			{
				fBarWidth = ChartControl.BarWidth;
				fBarDistance = ChartControl.Properties.BarDistance;
				
				topMenuItem3SubItem7.Visibility = Visibility.Visible;
				topMenuItem3SubItem8.Visibility = Visibility.Visible;
			}
			if(footprintDisplayType == FPV2FootprintDisplayType.Profile)
			{
				ChartControl.BarWidth = fBarWidth;
				ChartControl.Properties.BarDistance = fBarDistance;
				
				topMenuItem3SubItem7.Visibility = Visibility.Collapsed;
				topMenuItem3SubItem8.Visibility = Visibility.Collapsed;
			}
			
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
		
		#endregion
		
		#region refreshChart
		
		private void refreshChart()
		{
			try
			{
				chartWindow.ActiveChartControl.InvalidateVisual();
				ForceRefresh();
			}
			catch(Exception e)
			{
				if(log)
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
		
		#region OnMarketData
		
		/// OnMarketData
		///
		protected override void OnMarketData(MarketDataEventArgs marketDataUpdate)
		{
			if(!rdy) { return; }
			if(CurrentBars[0] == null) { return; }
			
			try
			{
				if(marketDataUpdate.MarketDataType == MarketDataType.Last)
				{
					double prc = Instrument.MasterInstrument.RoundToTickSize(marketDataUpdate.Price);
					double ask = Instrument.MasterInstrument.RoundToTickSize(marketDataUpdate.Ask);
					double bid = Instrument.MasterInstrument.RoundToTickSize(marketDataUpdate.Bid);
					double vol = marketDataUpdate.Volume;
					double avg = 0.0;
					
					if(prc >= ask)
					{
						if(showTapeStrip)
						{
							if(vol >= tapeStripFilter)
							{
								if(TapeStripItems.Count == 0)
								{
									TapeStripItems.Insert(0, new TapeStripItem(1, ask, vol));
								}
								else
								{
									if(TapeStripItems[0].dir != 1)
									{
										TapeStripItems.Insert(0, new TapeStripItem(1, ask, vol));
									}
									else
									{
										if(TapeStripItems[0].prc != ask)
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
						}
						
						BarItems[0].addAsk(prc, vol, prevProfileShow, currProfileShow, custProfileShow);
					}
					else if(prc <= bid)
					{
						if(showTapeStrip)
						{
							if(vol >= tapeStripFilter)
							{
								if(TapeStripItems.Count == 0)
								{
									TapeStripItems.Insert(0, new TapeStripItem(-1, bid, vol));
								}
								else
								{
									if(TapeStripItems[0].dir != -1)
									{
										TapeStripItems.Insert(0, new TapeStripItem(-1, bid, vol));
									}
									else
									{
										if(TapeStripItems[0].prc != bid)
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
						
						BarItems[0].addBid(prc, vol, prevProfileShow, currProfileShow, custProfileShow);
					}
					else
					{
						BarItems[0].addVol(prc, vol, prevProfileShow, currProfileShow, custProfileShow);
					}
					
					if(State == State.Historical && BarItems[0].clc == true && BarItems[1] != null)
					{
						BarItems[1].calc();
						
						if(custProfileShow)
						{
							BarItems[1].custProfile.calc();
						}
						if(currProfileShow || prevProfileShow)
						{
							BarItems[1].currProfile.calc();
						}
						
						BarItems[0].clc = false;
					}
					
					if(State == State.Realtime)
					{
						BarItems[0].calc();
						
						if(custProfileShow)
						{
							BarItems[0].custProfile.calc();
						}
						if(currProfileShow || prevProfileShow)
						{
							BarItems[0].currProfile.calc();
						}
					}
				}
				
				if(showTapeStrip && TapeStripItems.Count > tapeStripMaxItems)
				{
					TapeStripItems.RemoveRange(tapeStripMaxItems, TapeStripItems.Count - tapeStripMaxItems);
				}
			}
			catch(Exception exception)
			{
				if(log)
				{
					Print(exception.ToString());
				}
			}
		}
		
		#endregion
		
		#region OnBarUpdate
		
		/// OnBarUpdate
		///
		protected override void OnBarUpdate()
		{
			if(CurrentBar < 1) { return; }
			
			if(!Bars.IsTickReplay)
			{
				Draw.TextFixed(this, "noTickReplay", "Please enable Tick Replay...", TextPosition.Center);
				return;
			}
			
			rdy = true;
			
			if(paintBars)
			{
				if(ChartBars != null)
				{
					if(Close[0] > Open[0])
					{
						CandleOutlineBrush = bullishColor;
						BarBrush = bullishColor;
					}
					else if(Close[0] < Open[0])
					{
						CandleOutlineBrush = bearishColor;
						BarBrush = bearishColor;
					}
					else
					{
						CandleOutlineBrush = neutralColor;
						BarBrush = neutralColor;
					}
				}
			}
			
			#region ZZ
			
			if(CurrentBar == 0)
			{
				lastLoVal = Low[0];
				lastHiVal = High[0];
			}
			else
			{
				ZigZagLo[0] = MIN(Low, zzSpan)[0];
				ZigZagHi[0] = MAX(High, zzSpan)[0];
				
				if(zzDir == 0)
				{
					if(ZigZagLo[0] < lastLoVal)
					{
						lastLoVal = ZigZagLo[0];
						lastLoBar = CurrentBar;
						
						if(ZigZagHi[0] < ZigZagHi[1])
						{
							zzDir = -1;
						}
					}
					if(ZigZagHi[0] > lastHiVal)
					{
						lastHiVal = ZigZagHi[0];
						lastHiBar = CurrentBar;
						
						if(ZigZagLo[0] > ZigZagLo[1])
						{
							zzDir = 1;
						}
					}
				}
				
				if(zzDir > 0)
				{
					if(ZigZagHi[0] > lastHiVal)
					{
						ZigZagDots.Reset(CurrentBar-lastHiBar);
						
						lastHiVal = ZigZagHi[0];
						lastHiBar = CurrentBar;
						
						if(Plots[0].Brush != Brushes.Transparent)
						{
							Draw.Line(this, lastLoBar.ToString(), CurrentBar-lastLoBar, lastLoVal, CurrentBar-lastHiBar, lastHiVal, Plots[0].Brush);
						}
						
						ZigZagDots[CurrentBar-lastHiBar] = lastHiVal;
					}
					else if(ZigZagHi[0] < lastHiVal && ZigZagLo[0] < ZigZagLo[1])
					{
						if(Plots[0].Brush != Brushes.Transparent)
						{
							Draw.Line(this, lastLoBar.ToString(), CurrentBar-lastLoBar, lastLoVal, CurrentBar-lastHiBar, lastHiVal, Plots[0].Brush);
						}
						
						ZigZagDots[CurrentBar-lastHiBar] = lastHiVal;
						
						zzDir     = -1;
						lastLoVal = ZigZagLo[0];
						lastLoBar = CurrentBar;
						
						if(Plots[0].Brush != Brushes.Transparent)
						{
							Draw.Line(this, lastHiBar.ToString(), CurrentBar-lastHiBar, lastHiVal, CurrentBar-lastLoBar, lastLoVal, Plots[0].Brush);
						}
						
						ZigZagDots[CurrentBar-lastLoBar] = lastLoVal;
					}
				}
				else
				{
					if(ZigZagLo[0] < lastLoVal)
					{
						ZigZagDots.Reset(CurrentBar-lastLoBar);
						
						lastLoVal = ZigZagLo[0];
						lastLoBar = CurrentBar;
						
						if(Plots[0].Brush != Brushes.Transparent)
						{
							Draw.Line(this, lastHiBar.ToString(), CurrentBar-lastHiBar, lastHiVal, CurrentBar-lastLoBar, lastLoVal, Plots[0].Brush);
						}
						
						ZigZagDots[CurrentBar-lastLoBar] = lastLoVal;
					}
					else if(ZigZagLo[0] > lastLoVal && ZigZagHi[0] > ZigZagHi[1])
					{
						if(Plots[0].Brush != Brushes.Transparent)
						{
							Draw.Line(this, lastHiBar.ToString(), CurrentBar-lastHiBar, lastHiVal, CurrentBar-lastLoBar, lastLoVal, Plots[0].Brush);
						}
						
						ZigZagDots[CurrentBar-lastLoBar] = lastLoVal;
						
						zzDir     = 1;
						lastHiVal = ZigZagHi[0];
						lastHiBar = CurrentBar;
						
						if(Plots[0].Brush != Brushes.Transparent)
						{
							Draw.Line(this, lastLoBar.ToString(), CurrentBar-lastLoBar, lastLoVal, CurrentBar-lastHiBar, lastHiVal, Plots[0].Brush);
						}
						
						ZigZagDots[CurrentBar-lastHiBar] = lastHiVal;
					}
				}
			}
			
			#endregion
			
			try
			{
				if(BarItems[0] == null)
				{
					bool isFirstBarOfSession = Bars.IsFirstBarOfSession;
					
					BarItems[0] = new BarItem(CurrentBar, isFirstBarOfSession);
					
					if(BarItems[1] != null)
					{
						BarItems[0].avg = BarItems[1].avg;
						BarItems[1].cls = Close[1];
						
						if(custProfileShow)
						{
							BarItems[1].custProfile.cls = Close[1];
						}
						if(currProfileShow || prevProfileShow)
						{
							BarItems[1].currProfile.cls = Close[1];
						}
					}
					
					BarItems[0].opn = Open[0];
					BarItems[0].cls = Close[0];
					
					if(custProfileShow)
					{
						BarItems[0].custProfile.opn = Open[0];
						BarItems[0].custProfile.cls = Close[0];
					}
					
					if(currProfileShow || prevProfileShow)
					{
						BarItems[0].currProfile.opn = Open[0];
						BarItems[0].currProfile.cls = Close[0];
					}
					
					if(isFirstBarOfSession)
					{
						if(currProfileShow || prevProfileShow)
						{
							BarItems[0].currProfile.bar = CurrentBar;
						}
						
						if(prevProfileShow && BarItems[1] != null)
						{
							BarItems[0].prevProfile = BarItems[1].currProfile.Clone();
							BarItems[0].prevProfile.calc();
						}
					}
					else
					{
						if(BarItems[1] != null)
						{
							BarItems[0].cdo = BarItems[1].cdc;
							BarItems[0].cdl = BarItems[1].cdc;
							BarItems[0].cdh = BarItems[1].cdc;
							BarItems[0].cdc = BarItems[1].cdc;
							
							if(currProfileShow || prevProfileShow)
							{
								BarItems[0].currProfile = BarItems[1].currProfile.Clone();
							}
							
							if(prevProfileShow)
							{
								BarItems[0].prevProfile = BarItems[1].prevProfile;
							}
						}
						
						if(custProfileShow)
						{
							initCustomProfile(CurrentBar);
						}
					}
				}
			}
			catch(Exception exception)
			{
				if(log)
				{
					NinjaTrader.Code.Output.Process(exception.ToString(), PrintTo.OutputTab1);
				}
			}
		}
		
		#endregion
		
		#region initCustomProfile
		
		/// initCustomProfile
		///
		private void initCustomProfile(int bar)
		{
			if(BarItems[0] != null)
			{
				BarItems[0].custProfile.Reset();
				
				int    idx = bar;
				int    cnt = 0;
				double vol = 0.0;
				double min = double.MaxValue;
				double max = double.MinValue;
				double rng = 0.0;
				double pct = BarItems[0].currProfile.vol * (custProfilePctValue / 100.0);
				
				while(idx > 1)
				{
					if(BarItems.IsValidDataPointAt(idx))
					{
						BarItem barItem = BarItems.GetValueAt(idx);
						
						if(barItem != null)
						{
							BarItems[0].custProfile.min  = (barItem.min < BarItems[0].custProfile.min) ? barItem.min : BarItems[0].custProfile.min;
							BarItems[0].custProfile.max  = (barItem.max > BarItems[0].custProfile.max) ? barItem.max : BarItems[0].custProfile.max;
							BarItems[0].custProfile.rng  = BarItems[0].custProfile.max - BarItems[0].custProfile.min;
							BarItems[0].custProfile.opn  = barItem.opn;
							BarItems[0].custProfile.cls  = (BarItems[0].custProfile.cls == 0.0) ? barItem.cls : BarItems[0].custProfile.cls;
							BarItems[0].custProfile.vol += barItem.vol;
							BarItems[0].custProfile.ask += barItem.ask;
							BarItems[0].custProfile.bid += barItem.bid;
							BarItems[0].custProfile.dta += barItem.dtc;
							
							foreach(KeyValuePair<double, RowItem> ri in barItem.rowItems)
							{
								BarItems[0].custProfile.rowItems.GetOrAdd(ri.Key, new RowItem());
								
								BarItems[0].custProfile.rowItems[ri.Key].vol += ri.Value.vol;
								BarItems[0].custProfile.rowItems[ri.Key].ask += ri.Value.ask;
								BarItems[0].custProfile.rowItems[ri.Key].bid += ri.Value.bid;
								BarItems[0].custProfile.rowItems[ri.Key].dta += ri.Value.dta;
							}
							
							vol += barItem.vol;
							min = (barItem.min < min) ? barItem.min : min;
							max = (barItem.max > max) ? barItem.max : max;
							rng = (max - min) / TickSize;
							cnt++;
							
							if((vol >= pct && cnt >= custProfileBarValue && vol >= custProfileVolValue && rng >= custProfileRngValue) || barItem.ifb)
							{
								BarItems[0].custProfile.bar = idx;
								break;
							}
						}
					}
					
					idx--;
				}
				
				BarItems[0].custProfile.calc();
			}
		}
		
		#endregion
		
		#region Text Utilities
		
		/// getTextSze
		///
		private double getTextSize(ChartScale chartScale)
		{
			if(footprintDisplayType == FPV2FootprintDisplayType.Profile)
			{
				return ChartControl.Properties.LabelFont.Size;
			}
			
			float  y1 = 0f;
			float  y2 = 0f;
			float  ls = (float)ChartControl.Properties.LabelFont.Size*2;
			float  ts = float.MaxValue;
			double tp = Instrument.MasterInstrument.RoundToTickSize(chartScale.GetValueByY(ChartPanel.Y));
			double bt = Instrument.MasterInstrument.RoundToTickSize(chartScale.GetValueByY(ChartPanel.H));
			
			for(double i=bt-TickSize;i<=tp;i+=TickSize)
			{
				y1 = ((chartScale.GetYByValue(i) + chartScale.GetYByValue(i + TickSize)) / 2);
				y2 = ((chartScale.GetYByValue(i) + chartScale.GetYByValue(i - TickSize)) / 2);
				ts = (Math.Abs(y1 - y2) < ts) ? Math.Abs(y1 - y2) : ts;
			}
			
			return (double)Math.Min(Math.Floor(ts*0.5), ls);
		}
		
		/// getCellWidth
		///
		private int getCellWidth(double fontSize)
		{
			if(footprintDisplayType == FPV2FootprintDisplayType.Profile)
			{
				return (int)Math.Round((ChartControl.Properties.BarDistance - (barHalfWidth * 2) - 5) / 2);
			}
			
			BarItem currBarItem;
			
			double maxValue = 0.0;
			float  maxWidth = 0f;
			
			for(int i=ChartBars.ToIndex;i>=ChartBars.FromIndex;i--)
			{
				if(BarItems.IsValidDataPointAt(i))
				{
					currBarItem = BarItems.GetValueAt(i);
				}
				else
				{
					continue;
				}
				
				if(currBarItem == null)   { continue; }
				if(currBarItem.rowItems.IsEmpty) { continue; }
				
				foreach(KeyValuePair<double, RowItem> ri in currBarItem.rowItems)
				{
					maxValue = (ri.Value.ask > maxValue) ? ri.Value.ask : maxValue;
					maxValue = (ri.Value.bid > maxValue) ? ri.Value.bid : maxValue;
				}
			}
			
			if(maxValue > 0.0)
			{
				SimpleFont sf = new SimpleFont("Consolas", fontSize){ Bold = true };
				TextFormat tf = sf.ToDirectWriteTextFormat();
				TextLayout tl = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, maxValue.ToString(), tf, ChartPanel.W, ChartPanel.H);
				
				maxWidth = tl.Metrics.Width;
				
				sf = null;
				tf.Dispose();
				tl.Dispose();
			}
			
			return (int)Math.Round(maxWidth * 1.5);
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
			catch(Exception e)
			{
				if(log)
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
			    byte r = (byte) ((((SolidColorBrush)foreBrush).Color.R * amount) + ((SolidColorBrush)backBrush).Color.R * (1.0 - amount));
			    byte g = (byte) ((((SolidColorBrush)foreBrush).Color.G * amount) + ((SolidColorBrush)backBrush).Color.G * (1.0 - amount));
			    byte b = (byte) ((((SolidColorBrush)foreBrush).Color.B * amount) + ((SolidColorBrush)backBrush).Color.B * (1.0 - amount));
			    
				return new SolidColorBrush(Color.FromRgb(r, g, b));
			}
			catch(Exception e)
			{
				if(log)
				{
					Print(e.ToString());
				}
			}
			
			return Brushes.Yellow;
		}
		
		#endregion
		
		#region OnRender
		
		/// OnRender
		///
		protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			if(Bars == null || Bars.Instrument == null || ChartControl == null || IsInHitTest) { return; }
			
			base.OnRender(chartControl, chartScale);
			
			/// ---
			
			if(forceRefresh)
			{
				forceRefresh = false;
				
				refreshChart();
			}
			
			/// ---
			
			try
			{
				barFullWidth = chartControl.GetBarPaintWidth(ChartBars);
				barHalfWidth = ((barFullWidth - 1) / 2) + 2;
				
				dTextSize = (showFootprint) ? getTextSize(chartScale) : ChartControl.Properties.LabelFont.Size;
				cellWidth = (showFootprint) ? getCellWidth(dTextSize) : 0;
				
				/// ---
				
				int bmr = rightMargin;
				
				bmr += (prevProfileShow && prevProfileWidth > 0) ? prevProfileWidth + 30 : 0;
				bmr += (prevProfileShow && prevProfileWidth > 0 && prevProfileBar) ? 6 : 0;
				bmr += (prevProfileShow && prevProfileWidth > 0 && !prevProfileBar) ? 2 : 0;
				bmr += (currProfileShow && currProfileWidth > 0) ? currProfileWidth + 30 : 0;
				bmr += (currProfileShow && currProfileWidth > 0 && currProfileBar) ? 6 : 0;
				bmr += (currProfileShow && currProfileWidth > 0 && !currProfileBar) ? 2 : 0;
				bmr += (custProfileShow && custProfileWidth > 0) ? custProfileWidth + 42 : 0;
				bmr += (showFootprint && footprintDisplayType == FPV2FootprintDisplayType.Numbers && footprintDeltaProfile) ? (cellWidth + 2) : 0;
				bmr += (bmr > 0) ? 10 : 0;
				
				bmr += cellWidth;
				
				if(ChartControl.Properties.BarMarginRight != bmr)
				{
					ChartControl.Properties.BarMarginRight = bmr;
				}
				
				/// --- 
				
				drawMap(chartControl, chartScale);
				drawProfiles(chartControl, chartScale);
				drawClose(chartControl, chartScale);
				drawFootPrint(chartControl, chartScale);
				drawTapeStrip(chartControl, chartScale);
				drawBottomArea(chartControl, chartScale);
				
				/// ---
				
				if(chartScale.Properties.YAxisRangeType == YAxisRangeType.Fixed)
				{
					double mhi = Bars.GetHigh(ChartBars.ToIndex);
					double mlo = Bars.GetLow(ChartBars.ToIndex);
					double min = chartScale.Properties.FixedScaleMin;
					double max = chartScale.Properties.FixedScaleMax;
					double rng = max - min;
					double off = TickSize * 3;
					double dif = TickSize * 4;
					
					if(mhi >= max - off)
					{
						chartScale.Properties.FixedScaleMax = mhi + dif;
						chartScale.Properties.FixedScaleMin = (mhi + dif) - rng;
					}
					
					if(mlo <= min + off)
					{
						chartScale.Properties.FixedScaleMin = (mlo - dif);
						chartScale.Properties.FixedScaleMax = (mlo - dif) + rng;
					}
				}
			}
			catch(Exception exception)
			{
				if(log)
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
				if(!showClose) { return; }
				
				SharpDX.Direct2D1.AntialiasMode oldAntialiasMode = RenderTarget.AntialiasMode;
				RenderTarget.AntialiasMode = SharpDX.Direct2D1.AntialiasMode.Aliased;
				
				SharpDX.Direct2D1.Brush forBrush = neutralColor.ToDxBrush(RenderTarget);
				SharpDX.Direct2D1.Brush askBrush = bullishColor.ToDxBrush(RenderTarget);
				SharpDX.Direct2D1.Brush bidBrush = bearishColor.ToDxBrush(RenderTarget);
				
				SharpDX.RectangleF rect = new SharpDX.RectangleF();
				SharpDX.Vector2    vec1 = new SharpDX.Vector2();
				SharpDX.Vector2    vec2 = new SharpDX.Vector2();
				
				double ask = GetCurrentAsk();
				double bid = GetCurrentBid();
				double cls = Bars.GetClose(ChartBars.ToIndex);
				
				float x1,x2,y1,y2,wd,ht = 0f;
				
				/// ---
				
				x1 = chartControl.CanvasLeft;
				x2 = chartControl.CanvasRight;
				wd = Math.Abs(x1 - x2);
				
				/// area
				
				y1 = ((chartScale.GetYByValue(cls) + chartScale.GetYByValue(cls + TickSize)) / 2);
				y2 = ((chartScale.GetYByValue(cls) + chartScale.GetYByValue(cls - TickSize)) / 2);
				ht = Math.Abs(y2 - y1) - 1f;
				
				rect.X      = (float)x1;
				rect.Y      = (float)y1;
				rect.Width  = (float)wd;
				rect.Height = (float)ht;
				
				forBrush.Opacity = 0.1f;
				
				RenderTarget.FillRectangle(rect, forBrush);
				
				/// line
				
				y1 = chartScale.GetYByValue(cls);
				y2 = y1;
				
				vec1.X = x1;
				vec1.Y = y1;
				
				vec2.X = x2;
				vec2.Y = y2;
				
				if(cls >= ask)
				{
					askBrush.Opacity = 0.5f;
					
					RenderTarget.DrawLine(vec1, vec2, askBrush, 1, dLine.StrokeStyle);
				}
				else if(cls <= bid)
				{
					bidBrush.Opacity = 0.5f;
					
					RenderTarget.DrawLine(vec1, vec2, bidBrush, 1, dLine.StrokeStyle);
				}
				else
				{
					forBrush.Opacity = 0.5f;
				
					RenderTarget.DrawLine(vec1, vec2, forBrush, 1, dLine.StrokeStyle);	
				}
				
				/// ---
				
				forBrush.Dispose();
				askBrush.Dispose();
				bidBrush.Dispose();
				
				RenderTarget.AntialiasMode = oldAntialiasMode;
			}
			catch(Exception exception)
			{
				if(log)
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
				if(!custProfileShow || !custProfileMap) { return; }
				
				SharpDX.Direct2D1.AntialiasMode oldAntialiasMode = RenderTarget.AntialiasMode;
				RenderTarget.AntialiasMode = SharpDX.Direct2D1.AntialiasMode.Aliased;
				
				SolidColorBrush askColor = (SolidColorBrush)bullishColor.Clone();
				SolidColorBrush bidColor = (SolidColorBrush)bearishColor.Clone();
				
				SharpDX.Direct2D1.Brush mapBrush = mapColor.ToDxBrush(RenderTarget);
				SharpDX.Direct2D1.Brush askBrush = askColor.ToDxBrush(RenderTarget);
				SharpDX.Direct2D1.Brush bidBrush = bidColor.ToDxBrush(RenderTarget);
				
				SharpDX.RectangleF rect = new SharpDX.RectangleF();
				
				float x1,x2,y1,y2,wd,ht = 0f;
				
				float barWidth = (float)chartControl.GetBarPaintWidth(chartControl.BarsArray[0]);
					  barWidth = (showFootprint) ? (float)(barWidth + cellWidth * 2 + 4f) : barWidth;
				
				BarItem barItem;
				
				double sRng = 0.0;
				float  oRng = (custProfileMapType == FPV2MapDisplayType.Volume) ? mapMaxOpa : mapMaxOpa / 2;
				double size = 0.0;
				float  opac = 0.0f;
				double cont = 0.0;
				
				/// ---
				
				for(int i=ChartBars.ToIndex;i>=ChartBars.FromIndex-1;i--)
				{
					if(BarItems.IsValidDataPointAt(i))
					{
						barItem = BarItems.GetValueAt(i);
						
						if(barItem == null) { continue; }
						if(barItem.custProfile.rowItems.IsEmpty) { continue; }
						
						x1 = chartControl.GetXByBarIndex(ChartBars, i);
						x2 = chartControl.GetXByBarIndex(ChartBars, i + 1);
						
						if(i == ChartBars.ToIndex)
						{
							float rx = chartControl.CanvasRight - rightMargin;
								rx -= (prevProfileShow && prevProfileWidth > 0) ? prevProfileWidth + 30f : 0f;
								rx -= (prevProfileShow && prevProfileWidth > 0 && prevProfileBar) ? 6f : 0f;
								rx -= (prevProfileShow && prevProfileWidth > 0 && !prevProfileBar) ? 2f : 0f;
								rx -= (currProfileShow && currProfileWidth > 0) ? currProfileWidth + 30f : 0f;
								rx -= (currProfileShow && currProfileWidth > 0 && currProfileBar) ? 6f : 0f;
								rx -= (currProfileShow && currProfileWidth > 0 && !currProfileBar) ? 2f : 0f;
								rx -= (custProfileShow && custProfileWidth > 0) ? 9f : 0f;
							
							x2 = rx;
						}
						
						wd = Math.Abs(x1 - x2);
						
						sRng = (custProfileMapType == FPV2MapDisplayType.Volume) ? barItem.custProfile.rowItems[barItem.custProfile.poc].vol : barItem.custProfile.getMaxDta();
						size = 0.0;
						opac = 0.0f;
						cont = 0.0;
						
						foreach(KeyValuePair<double, RowItem> ri in barItem.custProfile.rowItems)
						{
							size = (custProfileMapType == FPV2MapDisplayType.Volume) ? ri.Value.vol : Math.Abs(ri.Value.dta);
							opac = (float)Math.Round((oRng / sRng) * size, 5);
							
							opac = (float)((opac - 0f) * (oRng / (oRng - 0f)));
							
							cont = (oRng - opac) * 2.0;
							opac = (float)((opac - 0f) * (oRng / ((oRng + cont) - 0f)));
							
							opac = (custProfileMapType == FPV2MapDisplayType.Volume && ri.Key == barItem.custProfile.poc) ? opac + 0.3f : opac;
							opac = Math.Min(1.0f, opac);
							
							if(opac > 0.01f)
							{
								y1 = ((chartScale.GetYByValue(ri.Key) + chartScale.GetYByValue(ri.Key + TickSize)) / 2);
								y2 = ((chartScale.GetYByValue(ri.Key) + chartScale.GetYByValue(ri.Key - TickSize)) / 2);
								
								ht = Math.Abs(y2 - y1);
								
								rect.X      = (float)x1;
								rect.Y      = (float)y1;
								rect.Width  = (float)wd;
								rect.Height = (float)ht;
								
								if(custProfileMapType == FPV2MapDisplayType.Volume)
								{
									mapBrush.Opacity = opac;
									RenderTarget.FillRectangle(rect, mapBrush);
								}
								else
								{
									if(ri.Value.dta > 0.0)
									{
										askBrush.Opacity = opac;
										RenderTarget.FillRectangle(rect, askBrush);
									}
									if(ri.Value.dta < 0.0)
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
				
				mapBrush.Dispose();
				askBrush.Dispose();
				bidBrush.Dispose();
				
				RenderTarget.AntialiasMode = oldAntialiasMode;
			}
			catch(Exception exception)
			{
				if(log)
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
			if(!BarItems.IsValidDataPointAt(ChartBars.ToIndex)) { return; }
			
			float rx = chartControl.CanvasRight - rightMargin;
			
			Profile prevProfile = BarItems.GetValueAt(ChartBars.ToIndex).prevProfile;
			Profile currProfile = BarItems.GetValueAt(ChartBars.ToIndex).currProfile;
			Profile custProfile = BarItems.GetValueAt(ChartBars.ToIndex).custProfile;
			
			if(prevProfileShow && !prevProfile.rowItems.IsEmpty)
			{
				drawProfile(prevProfile, rx, prevProfileWidth, prevProfileGradient, prevProfileValueArea, prevProfileExtendVa, prevProfileBar, false, prevProfileShowDelta, chartControl, chartScale);
			}
			
			rx -= (prevProfileShow) ? (prevProfileWidth + 30f) : 0;
			rx -= (prevProfileShow && prevProfileBar) ? 6f : 0f;
			rx -= (prevProfileShow && !prevProfileBar) ? 2f : 0f;
			
			if(currProfileShow && !currProfile.rowItems.IsEmpty)
			{
				drawProfile(currProfile, rx, currProfileWidth, currProfileGradient, currProfileValueArea, currProfileExtendVa, currProfileBar, false, currProfileShowDelta, chartControl, chartScale);
			}
			
			rx -= (currProfileShow) ? (currProfileWidth + 30f) : 0;
			rx -= (currProfileShow && currProfileBar) ? 6f : 0f;
			rx -= (currProfileShow && !currProfileBar) ? 2f : 0f;
			
			if(custProfileShow && !custProfile.rowItems.IsEmpty)
			{
				drawProfile(custProfile, rx, custProfileWidth, custProfileGradient, custProfileValueArea, custProfileExtendVa, false, true, custProfileShowDelta, chartControl, chartScale);
			}
		}
		
		#endregion
		
		#region drawProfile
		
		/// drawProfile
		///
		private void drawProfile(Profile profile, float rx, float profileWidth, bool gradient, bool valueArea, bool extendValueArea, bool bar, bool imbalances, bool showDelta, ChartControl chartControl, ChartScale chartScale)
		{
			try
			{
				if(profileWidth == 0) { return; }
				if(BarItems.GetValueAt(ChartBars.ToIndex) == null) { return; }
				
				SharpDX.Direct2D1.AntialiasMode oldAntialiasMode = RenderTarget.AntialiasMode;
				RenderTarget.AntialiasMode = SharpDX.Direct2D1.AntialiasMode.Aliased;
				
				SolidColorBrush bckColor = (SolidColorBrush)ChartControl.Properties.ChartBackground.Clone();
				
				SharpDX.Direct2D1.Brush bckBrush = bckColor.ToDxBrush(RenderTarget);
				SharpDX.Direct2D1.Brush forBrush = profileColor.ToDxBrush(RenderTarget);
				SharpDX.Direct2D1.Brush askBrush = bullishColor.ToDxBrush(RenderTarget);
				SharpDX.Direct2D1.Brush bidBrush = bearishColor.ToDxBrush(RenderTarget);
				SharpDX.Direct2D1.Brush pocBrush = pocColor.ToDxBrush(RenderTarget);
				
				SharpDX.RectangleF rect = new SharpDX.RectangleF();
				SharpDX.Vector2    vec1 = new SharpDX.Vector2();
				SharpDX.Vector2    vec2 = new SharpDX.Vector2();
				
				float x1,x2,y1,y2,wd,ht = 0f;
				
				double rngMin = chartScale.MinValue;
				double rngMax = chartScale.MaxValue;
				
				float barWidth = (bar) ? 6f : 2f;
				float imbWidth = (imbalances) ? 8f : 0f;
				
				float mh = float.MaxValue;
				
				/// ---
				
				#region Background
				
				if(
				!imbalances ||
				imbalances && !custProfileMap
				) {
					x1 = rx - profileWidth - barWidth - imbWidth;
					x2 = rx - imbWidth - barWidth + 1f;
					
					wd = Math.Abs(x2 - x1);
					
					y1 = ((chartScale.GetYByValue(profile.max) + chartScale.GetYByValue(profile.max + TickSize)) / 2);
					y2 = ((chartScale.GetYByValue(profile.min) + chartScale.GetYByValue(profile.min - TickSize)) / 2);
					
					ht = Math.Abs(y2 - y1);
					
					rect.X      = x1;
					rect.Y      = y1;
					rect.Width  = wd;
					rect.Height = ht;
					
					if(profile.dta > 0.0)
					{
						askBrush.Opacity = 0.03f;
						RenderTarget.FillRectangle(rect, askBrush);
					}
					else if(profile.dta < 0.0)
					{
						bidBrush.Opacity = 0.03f;
						RenderTarget.FillRectangle(rect, bidBrush);
					}
					else
					{
						forBrush.Opacity = 0.03f;
						RenderTarget.FillRectangle(rect, forBrush);
					}
				}
				
				#endregion
				
				#region Value Area
				
				if(valueArea)
				{
					if(extendValueArea)
					{
						/// vah
						
						x1 = rx - barWidth - imbWidth;
						x2 = chartControl.GetXByBarIndex(ChartBars, profile.bar - 1);
						
						wd = Math.Abs(x2 - x1);
						
						y1 = ((chartScale.GetYByValue(profile.vah) + chartScale.GetYByValue(profile.vah + TickSize)) / 2);
						y2 = ((chartScale.GetYByValue(profile.vah) + chartScale.GetYByValue(profile.vah - TickSize)) / 2);
						
						ht = Math.Abs(y2 - y1);
						mh = (ht < mh) ? ht : mh;
						
						rect.X      = x2;
						rect.Y      = y1;
						rect.Width  = wd;
						rect.Height = ht;
						
						if(bor)
						{
							rect.Width  -= 1f;
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
						
						rect.X      = x2;
						rect.Y      = y1;
						rect.Width  = wd;
						rect.Height = ht;
						
						if(bor)
						{
							rect.Width  -= 1f;
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
						
						rect.X      = x2;
						rect.Y      = y1;
						rect.Width  = wd;
						rect.Height = ht;
						
						if(bor)
						{
							rect.Width  -= 1f;
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
				
				double pocPrc = profile.poc;
				double pocVol = (profile.rowItems.ContainsKey(profile.poc)) ? profile.rowItems[profile.poc].vol : 0.0;
				
				double vahPrc = profile.vah;
				double vahVol = (profile.rowItems.ContainsKey(profile.vah)) ? profile.rowItems[profile.vah].vol : 0.0;
				
				double valPrc = profile.val;
				double valVol = (profile.rowItems.ContainsKey(profile.val)) ? profile.rowItems[profile.val].vol : 0.0;
				
				float  oRng = profileMaxOpa;
				float  opac = oRng;
				
				double dtc = 0.0;
				
				foreach(KeyValuePair<double, RowItem> ri in profile.rowItems)
				{
					if(ri.Key < rngMin || ri.Key > rngMax) { continue; }
					
					y1 = (chartScale.GetYByValue(ri.Key) + chartScale.GetYByValue(ri.Key + TickSize)) / 2;
					y2 = (chartScale.GetYByValue(ri.Key) + chartScale.GetYByValue(ri.Key - TickSize)) / 2;
					
					ht = Math.Abs(y2 - y1);
					mh = (ht < mh) ? ht : mh;
					
					wd = (float)Math.Round(((profileWidth) / pocVol) * ri.Value.vol);
					wd = Math.Max(2f, wd);
					
					opac = (gradient) ? (float)Math.Round((oRng / pocVol) * ri.Value.vol, 5) + 0.1f : oRng;
					
					rect.X      = (float)(rx - barWidth - imbWidth - wd);
					rect.Y      = (float)y1;
					rect.Width  = (float)wd;
					rect.Height = (float)ht;
					
					if(bor)
					{
						bckBrush.Opacity = 1.0f;
						
						RenderTarget.DrawRectangle(rect, bckBrush);
						RenderTarget.FillRectangle(rect, bckBrush);
						
						rect.Width  -= 1f;
						rect.Height -= 1f;
					}
					else
					{
						bckBrush.Opacity = 1.0f;
						
						RenderTarget.FillRectangle(rect, bckBrush);
					}
					
					forBrush.Opacity = opac;
					
					if(opac >= 0.01f)
					{
						if(ri.Key == pocPrc)
						{
							pocBrush.Opacity = opac;
							
							RenderTarget.FillRectangle(rect, pocBrush);
						}
						else
						{
							RenderTarget.FillRectangle(rect, forBrush);
						}
					}
					
					/// - value area
					
					if(valueArea)
					{
						if(ri.Key == profile.vah)
						{
							askBrush.Opacity = 0.3f;
							
							RenderTarget.FillRectangle(rect, askBrush);
						}
						
						if(ri.Key == profile.val)
						{
							bidBrush.Opacity = 0.3f;
							
							RenderTarget.FillRectangle(rect, bidBrush);
						}
					}
					
					/// - delta
					
					if(showDelta)
					{
						dtc = profile.getDelta(ri.Key);
						wd  = (float)Math.Round((wd / ri.Value.vol) * Math.Abs(dtc));
						
						if(wd >= 1f)
						{
							rect.X      = (float)(rx - barWidth - imbWidth - wd);
							rect.Y      = (float)y1;
							rect.Width  = (float)wd;
							rect.Height = (float)ht;
							
							if(bor)
							{
								rect.Width  -= 1f;
								rect.Height -= 1f;
							}
							
							if(dtc > 0.0)
							{
								askBrush.Opacity = 0.3f;
								
								RenderTarget.FillRectangle(rect, askBrush);
							}
							
							if(dtc < 0.0)
							{
								bidBrush.Opacity = 0.3f;
								
								RenderTarget.FillRectangle(rect, bidBrush);
							}
						}
					}
					
					/// - imbalances
					
					if(imbalances)
					{
						rect.X      = (float)(rx - imbWidth);
						rect.Y      = (float)y1;
						rect.Width  = 3f;
						rect.Height = (float)ht;
						
						if(bor)
						{
							rect.Height -= 1f;
						}
						
						if(profile.isBidImbalance(ri.Key + TickSize, ri.Key, minImbalanceRatio))
						{
							bidBrush.Opacity = 0.7f;
							
							RenderTarget.FillRectangle(rect, bidBrush);
						}
						else
						{
							forBrush.Opacity = 0.1f;
							
							RenderTarget.FillRectangle(rect, forBrush);
						}
						
						rect.X      = (float)(rx - imbWidth + 4f);
						rect.Y      = (float)y1;
						rect.Width  = 3f;
						rect.Height = (float)ht;
						
						if(bor)
						{
							rect.Height -= 1f;
						}
						
						if(profile.isAskImbalance(ri.Key, ri.Key - TickSize, minImbalanceRatio))
						{
							askBrush.Opacity = 0.7f;
							
							RenderTarget.FillRectangle(rect, askBrush);
						}
						else
						{
							forBrush.Opacity = 0.1f;
							
							RenderTarget.FillRectangle(rect, forBrush);
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
				
				forBrush.Opacity = 0.6f;
				askBrush.Opacity = 0.6f;
				bidBrush.Opacity = 0.6f;
				
				if(bar)
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
					
					if(profile.cls > profile.opn)
					{
						RenderTarget.DrawLine(vec1, vec2, askBrush, 1);
					}
					else if(profile.cls < profile.opn)
					{
						RenderTarget.DrawLine(vec1, vec2, bidBrush, 1);
					}
					else
					{
						RenderTarget.DrawLine(vec1, vec2, forBrush, 1);
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
					
					if(profile.cls > profile.opn)
					{
						RenderTarget.DrawLine(vec1, vec2, askBrush, 1);
					}
					else if(profile.cls < profile.opn)
					{
						RenderTarget.DrawLine(vec1, vec2, bidBrush, 1);
					}
					else
					{
						RenderTarget.DrawLine(vec1, vec2, forBrush, 1);
					}
					
					/// bar - upper wick
					
					x1 = rx - barWidth + 3f;
					
					y1 = ((chartScale.GetYByValue(profile.max) + chartScale.GetYByValue(profile.max + TickSize)) / 2);
					y2 = chartScale.GetYByValue(Math.Max(profile.opn, profile.cls));
					
					vec1.X = x1;
					vec1.Y = y1;
					
					vec2.X = x1;
					vec2.Y = y2;
					
					if(profile.cls > profile.opn)
					{
						RenderTarget.DrawLine(vec1, vec2, askBrush, 1);
					}
					else if(profile.cls < profile.opn)
					{
						RenderTarget.DrawLine(vec1, vec2, bidBrush, 1);
					}
					else
					{
						RenderTarget.DrawLine(vec1, vec2, forBrush, 1);
					}
					
					/// bar - body
					
					x1 = rx;
					
					y1 = chartScale.GetYByValue(Math.Max(profile.opn, profile.cls));
					y2 = chartScale.GetYByValue(Math.Min(profile.opn, profile.cls));
					
					ht = Math.Abs(y2 - y1);
					
					rect.X      = rx - barWidth + 1f;
					rect.Y      = y1;
					rect.Width  = 4f;
					rect.Height = ht;
					
					if(profile.cls > profile.opn)
					{
						RenderTarget.DrawRectangle(rect, askBrush);
						
						rect.Width  -= 1f;
						rect.Height -= 1f;
						
						RenderTarget.FillRectangle(rect, askBrush);
					}
					
					if(profile.cls < profile.opn)
					{
						RenderTarget.DrawRectangle(rect, bidBrush);
						
						rect.Width  -= 1f;
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
					
					if(profile.cls > profile.opn)
					{
						RenderTarget.DrawLine(vec1, vec2, askBrush, 1);
					}
					else if(profile.cls < profile.opn)
					{
						RenderTarget.DrawLine(vec1, vec2, bidBrush, 1);
					}
					else
					{
						RenderTarget.DrawLine(vec1, vec2, forBrush, 1);
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
					
					if(profile.dta > 0.0)
					{
						RenderTarget.DrawLine(vec1, vec2, askBrush, 1);
					}
					else if(profile.dta < 0.0)
					{
						RenderTarget.DrawLine(vec1, vec2, bidBrush, 1);
					}
					else
					{
						RenderTarget.DrawLine(vec1, vec2, forBrush, 1);
					}
					
					/// low
					
					vec1.X = x1;
					vec1.Y = y2;
					
					vec2.X = x2;
					vec2.Y = y2;
					
					if(profile.dta > 0.0)
					{
						RenderTarget.DrawLine(vec1, vec2, askBrush, 1);
					}
					else if(profile.dta < 0.0)
					{
						RenderTarget.DrawLine(vec1, vec2, bidBrush, 1);
					}
					else
					{
						RenderTarget.DrawLine(vec1, vec2, forBrush, 1);
					}
					
					/// ---
					
					vec1.X = x1;
					vec1.Y = y1;
					
					vec2.X = x1;
					vec2.Y = y2 - 1f;
					
					if(profile.dta > 0.0)
					{
						RenderTarget.DrawLine(vec1, vec2, askBrush, 1);
					}
					else if(profile.dta < 0.0)
					{
						RenderTarget.DrawLine(vec1, vec2, bidBrush, 1);
					}
					else
					{
						RenderTarget.DrawLine(vec1, vec2, forBrush, 1);
					}
				}
				
				#endregion
				
				#region Numbers
				
				tfNorm.TextAlignment = SharpDX.DirectWrite.TextAlignment.Trailing;
				tfNorm.WordWrapping	 = SharpDX.DirectWrite.WordWrapping.NoWrap;
				
				TextLayout textLayoutSum;
				TextLayout textLayoutDta;
				
				/// bot
				
				textLayoutSum = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, "Σ " + string.Format("{0:n0}", profile.vol), tfNorm, profileWidth, tfNorm.FontSize);
				textLayoutDta = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, "∆ " + string.Format("{0:n0}", profile.dta), tfNorm, profileWidth, tfNorm.FontSize);
				
				y1 = ((chartScale.GetYByValue(profile.min) + chartScale.GetYByValue(profile.min - TickSize)) / 2) + (float)(textLayoutSum.Metrics.Height / 2);
				
				RenderTarget.DrawTextLayout(new SharpDX.Vector2(rx - profileWidth - 6f, y1), textLayoutSum, forBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
				
				y1 = y1 + textLayoutSum.Metrics.Height;
				
				if(profile.dta > 0.0)
				{
					RenderTarget.DrawTextLayout(new SharpDX.Vector2(rx - profileWidth - 6f, y1), textLayoutDta, askBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
				}
				else
				{
					RenderTarget.DrawTextLayout(new SharpDX.Vector2(rx - profileWidth - 6f, y1), textLayoutDta, bidBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
				}
				
				/// top
				
				y1 = ((chartScale.GetYByValue(profile.max) + chartScale.GetYByValue(profile.max + TickSize)) / 2) - (float)(textLayoutSum.Metrics.Height * 1.5);
				
				RenderTarget.DrawTextLayout(new SharpDX.Vector2(rx - profileWidth - 6f, y1), textLayoutSum, forBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
				
				y1 = y1 - textLayoutSum.Metrics.Height;
				
				if(profile.dta > 0.0)
				{
					RenderTarget.DrawTextLayout(new SharpDX.Vector2(rx - profileWidth - 6f, y1), textLayoutDta, askBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
				}
				else
				{
					RenderTarget.DrawTextLayout(new SharpDX.Vector2(rx - profileWidth - 6f, y1), textLayoutDta, bidBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
				}
				
				#endregion
				
				bor = (mh >= 3f) ? true : false;
				
				/// ---
				
				textLayoutSum.Dispose();
				textLayoutDta.Dispose();
				
				bckBrush.Dispose();
				forBrush.Dispose();
				askBrush.Dispose();
				bidBrush.Dispose();
				pocBrush.Dispose();
				
				RenderTarget.AntialiasMode = oldAntialiasMode;
			}
			catch(Exception exception)
			{
				if(log)
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
			if(ChartBars.ToIndex < ChartBars.Count - 1) { return; }
			
			try
			{
				if(!showTapeStrip) { return; }
				if(TapeStripItems.Count < 1) { return; }
				
				SharpDX.Direct2D1.AntialiasMode oldAntialiasMode = RenderTarget.AntialiasMode;
				RenderTarget.AntialiasMode = SharpDX.Direct2D1.AntialiasMode.Aliased;
				
				SharpDX.Direct2D1.Brush askBrush = bullishColor.ToDxBrush(RenderTarget);
				SharpDX.Direct2D1.Brush bidBrush = bearishColor.ToDxBrush(RenderTarget);
				
				SharpDX.Vector2 vect = new SharpDX.Vector2();
				
				/// ---
				
				tfNorm.TextAlignment = SharpDX.DirectWrite.TextAlignment.Leading;
				tfNorm.WordWrapping	 = SharpDX.DirectWrite.WordWrapping.NoWrap;
				
				/// ---
				
				float x  = chartControl.GetXByBarIndex(ChartBars, ChartBars.ToIndex) + cellWidth + 10f;
					  x = (showFootprint && footprintDisplayType == FPV2FootprintDisplayType.Numbers && footprintDeltaProfile) ? x + (cellWidth + 2) : x;
				      x = (showFootprint) ? x + 2f : x;
				
				float y  = 0f;
				float wd = 0f;
				float ht = 0f;
				
				/// ---
				
				for(int i=0;i<TapeStripItems.Count;i++)
				{
					SharpDX.DirectWrite.TextLayout tl = new TextLayout(Core.Globals.DirectWriteFactory, TapeStripItems[i].vol.ToString(), tfNorm, ChartPanel.W, ChartPanel.H, 2, true);
					
					wd = tl.Metrics.Width;
					ht = tl.Metrics.Height;
					
					y = (float)(chartScale.GetYByValue(TapeStripItems[i].prc) - ht / 2) - 1f;
					
					/// ---
					
					vect.X = x;
					vect.Y = y;
					
					if(TapeStripItems[i].dir > 0)
					{
						askBrush.Opacity = 1f - (float)(i * (1f / tapeStripMaxItems));
						RenderTarget.DrawTextLayout(vect, tl, askBrush);
					}
					if(TapeStripItems[i].dir < 0)
					{
						bidBrush.Opacity = 1f - (float)(i * (1f / tapeStripMaxItems));
						RenderTarget.DrawTextLayout(vect, tl, bidBrush);
					}
					
					tl.Dispose();
					
					x += (wd + 5f);
				}
				
				/// ---
				
				askBrush.Dispose();
				bidBrush.Dispose();
				
				RenderTarget.AntialiasMode = oldAntialiasMode;
			}
			catch(Exception exception)
			{
				if(log)
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
			if(!showFootprint) { return; }
			
			int barDistance  = (int)(((cellWidth + barHalfWidth) * 2) + 5);
				barDistance += (footprintDisplayType == FPV2FootprintDisplayType.Numbers && footprintDeltaProfile) ? (cellWidth + 2) : 0;
			
			/// ---
			
			if(footprintDisplayType == FPV2FootprintDisplayType.Numbers && chartControl.Properties.BarDistance != barDistance)
			{
				chartControl.Properties.BarDistance = barDistance;
			}
			
			/// ---
			
			SharpDX.Direct2D1.AntialiasMode oldAntialiasMode = RenderTarget.AntialiasMode;
			RenderTarget.AntialiasMode = SharpDX.Direct2D1.AntialiasMode.Aliased;
			
			SolidColorBrush bckColor = (SolidColorBrush)ChartControl.Properties.ChartBackground.Clone();
			SolidColorBrush forColor = (SolidColorBrush)profileColor.Clone();
			SolidColorBrush askColor = (SolidColorBrush)bullishColor.Clone();
			SolidColorBrush bidColor = (SolidColorBrush)bearishColor.Clone();
			
			SharpDX.Direct2D1.Brush bckBrush = bckColor.ToDxBrush(RenderTarget);
			SharpDX.Direct2D1.Brush forBrush = forColor.ToDxBrush(RenderTarget);
			SharpDX.Direct2D1.Brush askBrush = askColor.ToDxBrush(RenderTarget);
			SharpDX.Direct2D1.Brush bidBrush = bidColor.ToDxBrush(RenderTarget);
			
			SimpleFont sfDynNorm = new SimpleFont("Consolas", dTextSize);
			SimpleFont sfDynBold = new SimpleFont("Consolas", dTextSize){ Bold = true };
			
			TextFormat tfDynNorm = sfDynNorm.ToDirectWriteTextFormat();
			TextFormat tfDynBold = sfDynBold.ToDirectWriteTextFormat();
			
			int   x1 = 0;
			int   x2 = 0;
			float y1 = 0;
			float y2 = 0;
			float tx = 0;
			float ty = 0;
			
			double poc = 0.0;
			double dtc = 0.0;
			double prc = Bars.GetClose(ChartBars.ToIndex);
			
			double askVol;
			double bidVol;
			double dtaVol;
			double maxVol = 0.0;
			double tmpVol = 0.0;
			double maxDta = 0.0;
			double tmpDta = 0.0;
			double barWidth;
			
			float  oRng = footprintMaxOpa;
			float  opac = oRng;
			
			TextLayout tl;
			
			ChartPanel chartPanel = chartControl.ChartPanels[chartScale.PanelIndex];
			
			BarItem currBarItem;
			
			SharpDX.RectangleF rect = new SharpDX.RectangleF();
			SharpDX.Vector2    vec1 = new SharpDX.Vector2();
			SharpDX.Vector2    vec2 = new SharpDX.Vector2();
			
			GlyphTypeface gtf 					= new GlyphTypeface();
			System.Windows.Media.Typeface tFace = new System.Windows.Media.Typeface(new System.Windows.Media.FontFamily(sfNorm.Family.ToString()), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            tFace.TryGetGlyphTypeface(out gtf);
			
			/// ---
			
			if(footprintRelativeVolume)
			{
				for(int i=ChartBars.FromIndex;i<=ChartBars.ToIndex;i++)
				{
					if(BarItems.IsValidDataPointAt(i))
					{
						currBarItem = BarItems.GetValueAt(i);
					}
					else
					{
						continue;
					}
					
					if(currBarItem == null)   { continue; }
					if(currBarItem.rowItems.IsEmpty) { continue; }
					
					tmpVol = currBarItem.getMaxVol();
					maxVol = (tmpVol > maxVol) ? tmpVol : maxVol;
					
					if(footprintDisplayType == FPV2FootprintDisplayType.Numbers && footprintDeltaProfile)
					{
						tmpDta = currBarItem.getMaxDta();
						maxDta = (tmpDta > maxDta) ? tmpDta : maxDta;
					}
				}
			}
			
			/// ---
			
			for(int i=ChartBars.FromIndex;i<=ChartBars.ToIndex;i++)
			{
				if(BarItems.IsValidDataPointAt(i))
				{
					currBarItem = BarItems.GetValueAt(i);
				}
				else
				{
					continue;
				}
				
				if(currBarItem == null)   { continue; }
				if(currBarItem.rowItems.IsEmpty) { continue; }
				
				x1 = chartControl.GetXByBarIndex(ChartBars, i);
				x2 = x1 + 20;
				
				poc = currBarItem.poc;
				dtc = 0.0;
				
				if(!footprintRelativeVolume)
				{
					maxVol = currBarItem.getMaxVol();
					
					if(footprintDisplayType == FPV2FootprintDisplayType.Numbers && footprintDeltaProfile)
					{
						maxDta = currBarItem.getMaxDta();
					}
				}
				
				/// draw background box
				
				y1 = ((chartScale.GetYByValue(currBarItem.max) + chartScale.GetYByValue(currBarItem.max + TickSize)) / 2) + 1;
				y2 = ((chartScale.GetYByValue(currBarItem.min) + chartScale.GetYByValue(currBarItem.min - TickSize)) / 2) - 1;
				
				rect.X      = (float)(x1 - barHalfWidth - cellWidth) - 1f;
				rect.Y      = (float)y1 - 1f;
				rect.Width  = (float)((barHalfWidth * 2) + (cellWidth * 2)) + 1f;
				rect.Height = (float)Math.Abs(y1 - y2) + 1f;
				
				#region delta outline
				
				if(footprintDeltaOutline)
				{
					if(showFootprint && !custProfileMap)
					{
						if(currBarItem.dtc > 0.0)
						{
							askBrush.Opacity = 0.04f;
							RenderTarget.FillRectangle(rect, askBrush);
						}
						else if(currBarItem.dtc < 0.0)
						{
							bidBrush.Opacity = 0.04f;
							RenderTarget.FillRectangle(rect, bidBrush);
						}
						else
						{
							forBrush.Opacity = 0.04f;
							RenderTarget.FillRectangle(rect, forBrush);
						}
					}
					
					/// ---
				
					vec1.X = rect.X ;
					vec1.Y = y1 - 2;
					
					vec2.X = rect.X + rect.Width;
					vec2.Y = y1 - 2;
					
					if(currBarItem.dtc < 0.0)
					{
						bidBrush.Opacity = 0.33f;
						RenderTarget.DrawLine(vec1, vec2, bidBrush, 1);
					}
					else
					{
						askBrush.Opacity = 0.33f;
						RenderTarget.DrawLine(vec1, vec2, askBrush, 1);
					}
					
					if(ZigZagDots.IsValidDataPointAt(i))
					{
						tfNorm.TextAlignment = SharpDX.DirectWrite.TextAlignment.Center;
						tfNorm.WordWrapping	 = SharpDX.DirectWrite.WordWrapping.NoWrap;
						
						if(ZigZagDots.GetValueAt(i) == Bars.GetHigh(i))
						{
							TextLayout textLayoutDta = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, "∆ " + string.Format("{0:n0}", currBarItem.cdh), tfNorm, rect.Width, tfNorm.FontSize);
							
							if(currBarItem.cdh < 0.0)
							{
								bidBrush.Opacity = 0.6f;
								RenderTarget.DrawTextLayout(new SharpDX.Vector2(rect.X, rect.Y - textLayoutDta.Metrics.Height - tfNorm.FontSize * 0.5f), textLayoutDta, bidBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
							}
							else
							{
								askBrush.Opacity = 0.6f;
								RenderTarget.DrawTextLayout(new SharpDX.Vector2(rect.X, rect.Y - textLayoutDta.Metrics.Height - tfNorm.FontSize * 0.5f), textLayoutDta, askBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
							}
							
							textLayoutDta.Dispose();
						}
						if(ZigZagDots.GetValueAt(i) == Bars.GetLow(i))
						{
							TextLayout textLayoutDta = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, "∆ " + string.Format("{0:n0}", currBarItem.cdl), tfNorm, rect.Width, tfNorm.FontSize);
							
							if(currBarItem.cdl < 0.0)
							{
								bidBrush.Opacity = 0.6f;
								RenderTarget.DrawTextLayout(new SharpDX.Vector2(rect.X, rect.Y + rect.Height + tfNorm.FontSize * 0.5f), textLayoutDta, bidBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
							}
							else
							{
								askBrush.Opacity = 0.6f;
								RenderTarget.DrawTextLayout(new SharpDX.Vector2(rect.X, rect.Y + rect.Height + tfNorm.FontSize * 0.5f), textLayoutDta, askBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
							}
							
							textLayoutDta.Dispose();
						}
					}
					
					vec1.X = rect.X;
					vec1.Y = y2 + 2;
					
					vec2.X = rect.X + rect.Width;
					vec2.Y = y2 + 2;
					
					if(currBarItem.dtc < 0.0)
					{
						bidBrush.Opacity = 0.33f;
						RenderTarget.DrawLine(vec1, vec2, bidBrush, 1);
					}
					else
					{
						askBrush.Opacity = 0.33f;
						RenderTarget.DrawLine(vec1, vec2, askBrush, 1);
					}
				}
				else
				{
					if(showFootprint && !custProfileMap)
					{
						forBrush.Opacity = 0.04f;
						RenderTarget.FillRectangle(rect, forBrush);
					}
				}
				
				#endregion
				
				foreach(KeyValuePair<double, RowItem> ri in currBarItem.rowItems)
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
					
					rect.X      = (float)(x1 + barHalfWidth);
					rect.Y      = (float)y1;
					rect.Width  = (float)barWidth;
					rect.Height = (float)Math.Abs(y1 - y2);
					
					/// ---
					
					bckBrush.Opacity = 1.0f;
					
					RenderTarget.DrawRectangle(rect, bckBrush);
					RenderTarget.FillRectangle(rect, bckBrush);
					
					/// ---
					
					if(footprintDisplayType == FPV2FootprintDisplayType.Numbers)
					{
						opac = (footprintGradient) ? (float)Math.Round((oRng / maxVol) * ri.Value.ask, 5) + 0.04f : 0.15f;
						opac = (!footprintGradient && ri.Key == poc) ? opac + 0.3f : opac;
						opac = (i == ChartBars.ToIndex && GetCurrentAsk() == ri.Key && ri.Key == prc) ? opac + 0.15f : opac;
						
						forBrush.Opacity = opac;
						
						RenderTarget.DrawRectangle(rect, forBrush);
						
						rect.Width  = rect.Width  - 1;
						rect.Height = rect.Height - 1;
						
						RenderTarget.FillRectangle(rect, forBrush);
						
						rect.Width  = rect.Width  + 1;
						rect.Height = rect.Height + 1;
					}
					else
					{
						opac = (footprintGradient) ? (float)Math.Round((oRng / maxVol) * ri.Value.ask, 5) + 0.04f : footprintMaxOpa;
						
						rect.Width = (float)barWidth;
						
						if(!footprintGradient && ri.Key == poc)
						{
							if(isAskImbalance)
							{
								askBrush.Opacity = Math.Min(1.0f, opac + 0.4f);
								RenderTarget.DrawRectangle(rect, askBrush);
								
								rect.Width  = rect.Width  - 1;
								rect.Height = rect.Height - 1;
								
								askBrush.Opacity = Math.Min(1.0f, opac + 0.4f);
								RenderTarget.FillRectangle(rect, askBrush);
							}
							else
							{
								forBrush.Opacity = Math.Min(1.0f, opac + 0.4f);
								RenderTarget.DrawRectangle(rect, forBrush);
								
								rect.Width  = rect.Width  - 1;
								rect.Height = rect.Height - 1;
								
								forBrush.Opacity = Math.Min(1.0f, opac + 0.4f);
								RenderTarget.FillRectangle(rect, forBrush);
							}
						}
						else if(isAskImbalance)
						{
							askBrush.Opacity = (footprintGradient) ? Math.Min(1.0f, opac + 0.2f) : Math.Min(1.0f, opac + 0.2f);
							RenderTarget.DrawRectangle(rect, askBrush);
							
							rect.Width  = rect.Width  - 1;
							rect.Height = rect.Height - 1;
							
							if(footprintGradient)
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
							forBrush.Opacity = opac;
							RenderTarget.DrawRectangle(rect, forBrush);
							
							rect.Width  = rect.Width  - 1;
							rect.Height = rect.Height - 1;
							
							forBrush.Opacity = opac;
							RenderTarget.FillRectangle(rect, forBrush);
						}
						
						rect.Width  = rect.Width  + 1;
						rect.Height = rect.Height + 1;
					}
					
					/// poc
					
					if(footprintDisplayType == FPV2FootprintDisplayType.Numbers)
					{
						if(ri.Key == poc)
						{
							forBrush.Opacity = 0.8f;
							
							vec1.X = rect.X + cellWidth - 1f;
							vec1.Y = y1 - 1f;
							
							vec2.X = rect.X + cellWidth - 1f;
							vec2.Y = y2;
							
							RenderTarget.DrawLine(vec1, vec2, forBrush, 3);
						}
						
						if(isAskImbalance)
						{
							askBrush.Opacity = (ri.Key == poc || (i == ChartBars.ToIndex && GetCurrentAsk() == ri.Key && ri.Key == prc)) ? 1.0f : 0.8f;
							
							vec1.X = rect.X + cellWidth;
							vec1.Y = y1 - 1f;
							
							vec2.X = rect.X + cellWidth;
							vec2.Y = y2;
							
							RenderTarget.DrawLine(vec1, vec2, askBrush, 1);
						}
					}
					
					/// ask - text
					
					if(footprintDisplayType == FPV2FootprintDisplayType.Numbers)
					{
						tfDynNorm.TextAlignment = SharpDX.DirectWrite.TextAlignment.Leading;
						tfDynBold.TextAlignment = SharpDX.DirectWrite.TextAlignment.Leading;
						
						tl = (ri.Key == poc) ? new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, askVol.ToString(), tfDynBold, rect.Width, rect.Height) : new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, askVol.ToString(), tfDynNorm, rect.Width, rect.Height);
						
						tl.ParagraphAlignment = SharpDX.DirectWrite.ParagraphAlignment.Center;
						
						/// ---
						
						vec1.X = rect.X + 2f;
						vec1.Y = rect.Y - 1f;
						
						/// ---
						
						opac = (footprintGradient) ? (float)Math.Round((oRng / maxVol) * ri.Value.ask, 5) + 0.3f : 0.45f;
						
						if(ri.Key == poc || isAskImbalance)
						{
							opac = 0.9f;
						}
						
						if(i == ChartBars.ToIndex && GetCurrentAsk() == ri.Key && ri.Key == prc)
						{
							opac += 0.15f;
						}
						
						opac = Math.Min(1.0f, opac);
						
						/// ---
						
						if(isAskImbalance)
						{
							askBrush.Opacity = opac;
							RenderTarget.DrawTextLayout(vec1, tl, askBrush);
						}
						else
						{
							forBrush.Opacity = opac;
							RenderTarget.DrawTextLayout(vec1, tl, forBrush);
						}
						
						/// ---
						
						tl.Dispose();
					}
					
					#endregion
					
					#region delta profile
					
					if(footprintDisplayType == FPV2FootprintDisplayType.Numbers && footprintDeltaProfile)
					{
						barWidth = (cellWidth / maxDta) * Math.Abs(dtaVol);
						barWidth = Math.Round(barWidth);
						
						if(barWidth >= 1)
						{
							rect.X      = (float)(x1 + barHalfWidth + cellWidth + 2);
							rect.Y      = (float)y1;
							rect.Width  = (float)barWidth;
							rect.Height = (float)Math.Abs(y1 - y2);
							
							/// ---
							
							bckBrush.Opacity = 1.0f;
							
							RenderTarget.DrawRectangle(rect, bckBrush);
							RenderTarget.FillRectangle(rect, bckBrush);
							
							/// ---
							
							opac = (footprintDeltaGradient) ? (float)((0.5f / maxDta) * Math.Abs(dtaVol)) + 0.1f : 0.5f;
							
							if(dtaVol > 0.0)
							{
								askBrush.Opacity = opac;
								RenderTarget.DrawRectangle(rect, askBrush);
								
								rect.Width  = rect.Width  - 1;
								rect.Height = rect.Height - 1;
								
								RenderTarget.FillRectangle(rect, askBrush);
							}
							if(dtaVol < 0.0)
							{
								bidBrush.Opacity = opac;
								RenderTarget.DrawRectangle(rect, bidBrush);
								
								rect.Width  = rect.Width  - 1;
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
					
					rect.X      = (float)(x1 - barHalfWidth - barWidth);
					rect.Y      = (float)y1;
					rect.Width  = (float)barWidth;
					rect.Height = (float)Math.Abs(y1 - y2);
					
					/// ---
					
					bckBrush.Opacity = 1.0f;
						
					RenderTarget.DrawRectangle(rect, bckBrush);
					RenderTarget.FillRectangle(rect, bckBrush);
					
					/// ---
					
					if(footprintDisplayType == FPV2FootprintDisplayType.Numbers)
					{
						opac = (footprintGradient) ? (float)Math.Round((oRng / maxVol) * ri.Value.bid, 5) + 0.04f : 0.15f;
						opac = (!footprintGradient && ri.Key == poc) ? opac + 0.3f : opac;
						opac = (i == ChartBars.ToIndex && GetCurrentBid() == ri.Key && ri.Key == prc) ? opac + 0.15f : opac;
						
						forBrush.Opacity = opac;
						
						RenderTarget.DrawRectangle(rect, forBrush);
						
						rect.Width  = rect.Width  - 1;
						rect.Height = rect.Height - 1;
						
						RenderTarget.FillRectangle(rect, forBrush);
						
						rect.Width  = rect.Width  + 1;
						rect.Height = rect.Height + 1;
					}
					else
					{
						opac = (footprintGradient) ? (float)Math.Round((oRng / maxVol) * ri.Value.bid, 5) + 0.04f : footprintMaxOpa;
						
						rect.Width = (float)barWidth;
						
						if(!footprintGradient && ri.Key == poc)
						{
							if(isBidImbalance)
							{
								bidBrush.Opacity = Math.Min(1.0f, opac + 0.4f);
								RenderTarget.DrawRectangle(rect, bidBrush);
								
								rect.Width  = rect.Width  - 1;
								rect.Height = rect.Height - 1;
								
								bidBrush.Opacity = Math.Min(1.0f, opac + 0.4f);
								RenderTarget.FillRectangle(rect, bidBrush);
							}
							else
							{
								forBrush.Opacity = Math.Min(1.0f, opac + 0.4f);
								RenderTarget.DrawRectangle(rect, forBrush);
								
								rect.Width  = rect.Width  - 1;
								rect.Height = rect.Height - 1;
								
								forBrush.Opacity = Math.Min(1.0f, opac + 0.4f);
								RenderTarget.FillRectangle(rect, forBrush);
							}
						}
						else if(isBidImbalance)
						{
							bidBrush.Opacity = (footprintGradient) ? Math.Min(1.0f, opac + 0.2f) : Math.Min(1.0f, opac + 0.2f);
							RenderTarget.DrawRectangle(rect, bidBrush);
							
							rect.Width  = rect.Width  - 1;
							rect.Height = rect.Height - 1;
							
							if(footprintGradient)
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
							forBrush.Opacity = opac;
							RenderTarget.DrawRectangle(rect, forBrush);
							
							rect.Width  = rect.Width  - 1;
							rect.Height = rect.Height - 1;
							
							forBrush.Opacity = opac;
							RenderTarget.FillRectangle(rect, forBrush);
						}
						
						rect.Width  = rect.Width  + 1;
						rect.Height = rect.Height + 1;
					}
					
					/// poc
					
					if(footprintDisplayType == FPV2FootprintDisplayType.Numbers)
					{
						if(ri.Key == poc)
						{
							forBrush.Opacity = 0.8f;
							
							vec1.X = rect.X + 1f;
							vec1.Y = y1 - 1f;
							
							vec2.X = rect.X + 1f;
							vec2.Y = y2;
							
							RenderTarget.DrawLine(vec1, vec2, forBrush, 3);
						}
						
						if(isBidImbalance)
						{
							bidBrush.Opacity = (ri.Key == poc || (i == ChartBars.ToIndex && GetCurrentBid() == ri.Key && ri.Key == prc)) ? 1.0f : 0.8f;
							
							vec1.X = rect.X;
							vec1.Y = y1 - 1f;
							
							vec2.X = rect.X;
							vec2.Y = y2;
							
							RenderTarget.DrawLine(vec1, vec2, bidBrush, 1);
						}
					}
					
					/// bid - text
					
					if(footprintDisplayType == FPV2FootprintDisplayType.Numbers)
					{
						tfDynNorm.TextAlignment = SharpDX.DirectWrite.TextAlignment.Trailing;
						tfDynBold.TextAlignment = SharpDX.DirectWrite.TextAlignment.Trailing;
						
						tl = (ri.Key == poc) ? new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, bidVol.ToString(), tfDynBold, rect.Width, rect.Height) : new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, bidVol.ToString(), tfDynNorm, rect.Width, rect.Height);
						
						tl.ParagraphAlignment = SharpDX.DirectWrite.ParagraphAlignment.Center;
						
						/// ---
						
						vec1.X = rect.X - 3f;
						vec1.Y = rect.Y - 1f;
						
						/// ---
						
						opac = (footprintGradient) ? (float)Math.Round((oRng / maxVol) * ri.Value.bid, 5) + 0.3f : 0.45f;
						
						if(ri.Key == poc || isBidImbalance)
						{
							opac = 0.9f;
						}
						
						if(i == ChartBars.ToIndex && GetCurrentBid() == ri.Key && ri.Key == prc)
						{
							opac += 0.15f;
						}
						
						opac = Math.Min(1.0f, opac);
						
						/// ---
						
						if(isBidImbalance)
						{
							bidBrush.Opacity = opac;
							RenderTarget.DrawTextLayout(vec1, tl, bidBrush);
						}
						else
						{
							forBrush.Opacity = opac;
							RenderTarget.DrawTextLayout(vec1, tl, forBrush);
						}
						
						/// ---
						
						tl.Dispose();
					}
					
					#endregion
				}
			}
			
			/// ---
			
			chartPanel	= null;
			currBarItem	= null;
			gtf			= null;
			tFace		= null;
			
			/// ---
			
			sfDynNorm = null;
			sfDynBold = null;
			tfDynNorm.Dispose();
			tfDynBold.Dispose();
			
			/// ---
			
			bckBrush.Dispose();
			forBrush.Dispose();
			askBrush.Dispose();
			bidBrush.Dispose();
			
			RenderTarget.AntialiasMode = oldAntialiasMode;
		}
		
		#endregion
		
		#region drawBottomArea
		
		/// drawBottomArea
		///
		private void drawBottomArea(ChartControl chartControl, ChartScale chartScale)
		{
			if(!showBottomArea) { return; }
			
			int barDistance = (int)(((cellWidth + barHalfWidth) * 2) + 5);
			
			/// ---
			
			SharpDX.Direct2D1.AntialiasMode oldAntialiasMode = RenderTarget.AntialiasMode;
			RenderTarget.AntialiasMode = SharpDX.Direct2D1.AntialiasMode.Aliased;
			
			SolidColorBrush bckColor = (SolidColorBrush)ChartControl.Properties.ChartBackground.Clone();
			
			SharpDX.Direct2D1.Brush bckBrush = bckColor.ToDxBrush(RenderTarget);
			SharpDX.Direct2D1.Brush forBrush = neutralColor.ToDxBrush(RenderTarget);
			SharpDX.Direct2D1.Brush askBrush = bullishColor.ToDxBrush(RenderTarget);
			SharpDX.Direct2D1.Brush bidBrush = bearishColor.ToDxBrush(RenderTarget);
			
			ChartPanel chartPanel = chartControl.ChartPanels[chartScale.PanelIndex];
			
			BarItem currBarItem;
			
			SharpDX.RectangleF rect = new SharpDX.RectangleF();
			SharpDX.Vector2    vec1 = new SharpDX.Vector2();
			SharpDX.Vector2    vec2 = new SharpDX.Vector2();
			
			/// ---
			
			double minPrc = double.MaxValue;
			float  maxPix = 0f;
			
			for(int i=ChartBars.FromIndex;i<=ChartBars.ToIndex;i++)
			{
				minPrc = (Bars.GetLow(i) < minPrc) ? Bars.GetLow(i) : minPrc;
			}
			
			maxPix = (float)(chartPanel.H - chartScale.GetYByValue(minPrc) - tfNorm.FontSize * 2.5);
			
			if(maxPix < 10f)
			{
				return;
			}
			
			/// ---
			
			int    x1	  = 0;
			int    x2 	  = 0;
			float  y1 	  = 0f;
			float  y2 	  = 0f;
			float  wt	  = 0f;
			float  ht	  = 0f;
			double minDta = double.MaxValue;
			double maxDta = double.MinValue;
			double maxVol = 0.0;
			double dtaRng = 0.0;
			double barWid = chartControl.GetBarPaintWidth(chartControl.BarsArray[0]);
			double factor = 0.0;
			
			#region cumulative delta
			
			if(bottomAreaType == FPV2BottomAreaType.CumulativeDelta)
			{
				for(int i=ChartBars.FromIndex;i<=ChartBars.ToIndex;i++)
				{
					if(BarItems.IsValidDataPointAt(i))
					{
						currBarItem = BarItems.GetValueAt(i);
					}
					else
					{
						continue;
					}
					
					if(currBarItem == null) { continue; }
					if(currBarItem.rowItems.IsEmpty) { continue; }
					
					minDta = (currBarItem.cdl < minDta) ? currBarItem.cdl : minDta;
					maxDta = (currBarItem.cdh > maxDta) ? currBarItem.cdh : maxDta;
				}
				
				dtaRng = Math.Abs(maxDta - minDta);
				factor = maxPix / dtaRng;
				
				/// panel background
				
				x1 = chartControl.GetXByBarIndex(ChartBars, ChartBars.ToIndex);
				wt = (showFootprint) ? (float)(x1 + barHalfWidth + cellWidth) : (float)(x1 + (barWid / 2));
				ht = maxPix;
				
				if(minDta > 0.0)
				{
					rect.X      = (float)(chartPanel.X);
					rect.Y      = (float)(chartPanel.H - ht);
					rect.Width  = wt;
					rect.Height = ht;
					
					askBrush.Opacity = 0.04f;
					RenderTarget.FillRectangle(rect, askBrush);
				}
				if(maxDta < 0.0)
				{
					rect.X      = (float)(chartPanel.X);
					rect.Y      = (float)(chartPanel.H - ht);
					rect.Width  = wt;
					rect.Height = ht;
					
					bidBrush.Opacity = 0.04f;
					RenderTarget.FillRectangle(rect, bidBrush);
				}
				if(minDta < 0.0 && maxDta > 0.0)
				{
					rect.X      = (float)(chartPanel.X);
					rect.Y      = (float)(chartPanel.H - ((0.0 - minDta) * factor));
					rect.Width  = wt;
					rect.Height = (float)((0.0 - minDta) * factor);
					
					bidBrush.Opacity = 0.04f;
					RenderTarget.FillRectangle(rect, bidBrush);
					
					rect.X      = (float)(chartPanel.X);
					rect.Y      = (float)(chartPanel.H - ((maxDta - minDta) * factor));
					rect.Width  = wt;
					rect.Height = (float)(maxPix - ((0.0 - minDta) * factor));
					
					askBrush.Opacity = 0.04f;
					RenderTarget.FillRectangle(rect, askBrush);
					
					vec1.X = (float)(chartPanel.X);
					vec1.Y = (float)(chartPanel.H - ((0.0 - minDta) * factor));
					
					vec2.X = (float)(chartPanel.X + wt);
					vec2.Y = (float)(chartPanel.H - ((0.0 - minDta) * factor));
					
					forBrush.Opacity = 0.1f;
					RenderTarget.DrawLine(vec1, vec2, forBrush, 1);
				}
				
				/// ---
				
				for(int i=ChartBars.FromIndex;i<=ChartBars.ToIndex;i++)
				{
					if(BarItems.IsValidDataPointAt(i))
					{
						currBarItem = BarItems.GetValueAt(i);
					}
					else
					{
						continue;
					}
					
					if(currBarItem == null)   { continue; }
					
					/// body
					
					x1 = chartControl.GetXByBarIndex(ChartBars, i);
					y1 = (float)((currBarItem.cdo - minDta) * factor);
					y2 = (float)((currBarItem.cdc - minDta) * factor);
					ht = y1 - y2;
					
					if(showFootprint)
					{
						rect.X      = (float)(x1 - barHalfWidth - cellWidth) - 1f;
						rect.Y      = (float)(chartPanel.H - y1);
						rect.Width  = (float)((barHalfWidth * 2) + (cellWidth * 2)) + 1f;
						rect.Height = (ht > 0f) ? Math.Max(1f, ht) : Math.Min(-1f, ht);
					}
					else
					{
						rect.X      = (float)(x1 - (barWid / 2));
						rect.Y      = (float)(chartPanel.H - y1);
						rect.Width  = (float)(barWid);
						rect.Height = (ht > 0f) ? Math.Max(1f, ht) : Math.Min(-1f, ht);
					}
					
					bckBrush.Opacity = 1.0f;
					RenderTarget.FillRectangle(rect, bckBrush);
					
					if(currBarItem.dtc > 0.0)
					{
						askBrush.Opacity = 0.6f;
						RenderTarget.FillRectangle(rect, askBrush);
					}
					
					if(currBarItem.dtc < 0.0)
					{
						bidBrush.Opacity = 0.6f;
						RenderTarget.FillRectangle(rect, bidBrush);
					}
					
					if(currBarItem.dtc == 0.0)
					{
						forBrush.Opacity = 0.6f;
						RenderTarget.FillRectangle(rect, forBrush);
					}
					
					/// upper wick
					
					vec1.X = x1;
					vec1.Y = (float)(chartPanel.H - (currBarItem.cdh - minDta) * factor);
					
					vec2.X = x1;
					vec2.Y = (float)Math.Min(chartPanel.H - (currBarItem.cdo - minDta) * factor, chartPanel.H - (currBarItem.cdc - minDta) * factor);
					
					if(currBarItem.dtc > 0.0)
					{
						askBrush.Opacity = 0.6f;
						RenderTarget.DrawLine(vec1, vec2, askBrush, 1);
					}
					
					if(currBarItem.dtc < 0.0)
					{
						bidBrush.Opacity = 0.6f;
						RenderTarget.DrawLine(vec1, vec2, bidBrush, 1);
					}
					
					if(currBarItem.dtc == 0.0)
					{
						forBrush.Opacity = 0.6f;
						RenderTarget.DrawLine(vec1, vec2, forBrush, 1);
					}
					
					/// lower wick
					
					vec1.X = x1;
					vec1.Y = (float)Math.Max(chartPanel.H - (currBarItem.cdo - minDta) * factor, chartPanel.H - (currBarItem.cdc - minDta) * factor);
					
					vec2.X = x1;
					vec2.Y = (float)(chartPanel.H - (currBarItem.cdl - minDta) * factor);
					
					if(currBarItem.dtc > 0.0)
					{
						askBrush.Opacity = 0.6f;
						RenderTarget.DrawLine(vec1, vec2, askBrush, 1);
					}
					
					if(currBarItem.dtc < 0.0)
					{
						bidBrush.Opacity = 0.6f;
						RenderTarget.DrawLine(vec1, vec2, bidBrush, 1);
					}
					
					if(currBarItem.dtc == 0.0)
					{
						forBrush.Opacity = 0.6f;
						RenderTarget.DrawLine(vec1, vec2, forBrush, 1);
					}
				}
			}
			
			#endregion
			
			#region delta
			
			if(bottomAreaType == FPV2BottomAreaType.Delta)
			{
				for(int i=ChartBars.FromIndex;i<=ChartBars.ToIndex;i++)
				{
					if(BarItems.IsValidDataPointAt(i))
					{
						currBarItem = BarItems.GetValueAt(i);
					}
					else
					{
						continue;
					}
					
					if(currBarItem == null) { continue; }
					if(currBarItem.rowItems.IsEmpty) { continue; }
					
					maxDta = (Math.Abs(currBarItem.dtc) > maxDta) ? Math.Abs(currBarItem.dtc) : maxDta;
				}
				
				/// ---
			
				for(int i=ChartBars.FromIndex;i<=ChartBars.ToIndex;i++)
				{
					if(BarItems.IsValidDataPointAt(i))
					{
						currBarItem = BarItems.GetValueAt(i);
					}
					else
					{
						continue;
					}
					
					if(currBarItem == null)   { continue; }
					if(currBarItem.dtc == 0) { continue; }
					
					x1 = chartControl.GetXByBarIndex(ChartBars, i);
					ht = (float)Math.Round((maxPix / maxDta) * Math.Abs(currBarItem.dtc));
					
					if(showFootprint)
					{
						rect.X      = (float)(x1 - barHalfWidth - cellWidth) - 1f;
						rect.Y      = (float)(chartPanel.H - ht);
						rect.Width  = (float)((barHalfWidth * 2) + (cellWidth * 2)) + 1f;
						rect.Height = ht;
					}
					else
					{
						rect.X      = (float)(x1 - (barWid / 2));
						rect.Y      = (float)(chartPanel.H - ht);
						rect.Width  = (float)(barWid);
						rect.Height = ht;
					}
					
					bckBrush.Opacity = 1.0f;
					RenderTarget.FillRectangle(rect, bckBrush);
					
					if(currBarItem.dtc > 0.0)
					{
						askBrush.Opacity = (bottomAreaGradient) ? (float)((0.5f / maxDta) * Math.Abs(currBarItem.dtc)) + 0.1f : 0.6f;
						RenderTarget.FillRectangle(rect, askBrush);
					}
					
					if(currBarItem.dtc < 0.0)
					{
						bidBrush.Opacity = (bottomAreaGradient) ? (float)((0.5f / maxDta) * Math.Abs(currBarItem.dtc)) + 0.1f : 0.6f;
						RenderTarget.FillRectangle(rect, bidBrush);
					}
				}
			}
			
			#endregion
			
			#region volume
			
			if(bottomAreaType == FPV2BottomAreaType.Volume)
			{
				for(int i=ChartBars.FromIndex;i<=ChartBars.ToIndex;i++)
				{
					if(BarItems.IsValidDataPointAt(i))
					{
						currBarItem = BarItems.GetValueAt(i);
					}
					else
					{
						continue;
					}
					
					if(currBarItem == null) { continue; }
					if(currBarItem.rowItems.IsEmpty) { continue; }
					
					maxVol = (Math.Abs(currBarItem.vol) > maxVol) ? Math.Abs(currBarItem.vol) : maxVol;
				}
				
				/// ---
			
				for(int i=ChartBars.FromIndex;i<=ChartBars.ToIndex;i++)
				{
					if(BarItems.IsValidDataPointAt(i))
					{
						currBarItem = BarItems.GetValueAt(i);
					}
					else
					{
						continue;
					}
					
					if(currBarItem == null)   { continue; }
					if(currBarItem.dtc == 0) { continue; }
					
					x1 = chartControl.GetXByBarIndex(ChartBars, i);
					ht = (float)Math.Round((maxPix / maxVol) * Math.Abs(currBarItem.vol));
					
					if(showFootprint)
					{
						rect.X      = (float)(x1 - barHalfWidth - cellWidth) - 1f;
						rect.Y      = (float)(chartPanel.H - ht);
						rect.Width  = (float)((barHalfWidth * 2) + (cellWidth * 2)) + 1f;
						rect.Height = ht;
					}
					else
					{
						rect.X      = (float)(x1 - (barWid / 2));
						rect.Y      = (float)(chartPanel.H - ht);
						rect.Width  = (float)(barWid);
						rect.Height = ht;
					}
					
					if(rect.Height >= 1f)
					{
						bckBrush.Opacity = 1.0f;
						RenderTarget.FillRectangle(rect, bckBrush);
						
						if(currBarItem.cls > currBarItem.opn)
						{
							askBrush.Opacity = (bottomAreaGradient) ? (float)((0.5f / maxVol) * Math.Abs(currBarItem.vol)) + 0.1f : 0.6f;
							RenderTarget.FillRectangle(rect, askBrush);
						}
						else if(currBarItem.cls < currBarItem.opn)
						{
							bidBrush.Opacity = (bottomAreaGradient) ? (float)((0.5f / maxVol) * Math.Abs(currBarItem.vol)) + 0.1f : 0.6f;
							RenderTarget.FillRectangle(rect, bidBrush);
						}
						else
						{
							forBrush.Opacity = (bottomAreaGradient) ? (float)((0.5f / maxVol) * Math.Abs(currBarItem.vol)) + 0.1f : 0.6f;
							RenderTarget.FillRectangle(rect, forBrush);
						}
					}
				}
			}
			
			#endregion
			
			/// ---
			
			chartPanel	= null;
			currBarItem	= null;
			
			/// ---
			
			bckBrush.Dispose();
			forBrush.Dispose();
			askBrush.Dispose();
			bidBrush.Dispose();
			
			RenderTarget.AntialiasMode = oldAntialiasMode;
		}
		
		#endregion
		
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
        [Display(Name = "Show Close", GroupName = "General", Order = 1)]
        public bool showClose
        { get; set; }
		
		/// ---
		
		[NinjaScriptProperty]
        [Display(Name = "Paint Bars", GroupName = "General", Order = 2)]
        public bool paintBars
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
        [Display(Name = "Bar", GroupName = "Profile (Previous Session)", Order = 6)]
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
        [Display(Name = "Bar", GroupName = "Profile (Current Session)", Order = 6)]
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
        [Range(0.0, 100.0)]
        [Display(Name = "Percent Value", GroupName = "Profile (Custom)", Order = 6)]
        public double custProfilePctValue
        { get; set; }
		
		/// ---
		
		[NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Bar Value", GroupName = "Profile (Custom)", Order = 7)]
        public int custProfileBarValue
        { get; set; }
		
		/// ---
		
		[NinjaScriptProperty]
        [Range(1.0, double.MaxValue)]
        [Display(Name = "Volume Value", GroupName = "Profile (Custom)", Order = 8)]
        public double custProfileVolValue
        { get; set; }
		
		/// ---
		
		[NinjaScriptProperty]
        [Range(1.0, double.MaxValue)]
        [Display(Name = "Range Value (Ticks)", GroupName = "Profile (Custom)", Order = 9)]
        public double custProfileRngValue
        { get; set; }
		
		/// ---
		
		[NinjaScriptProperty]
        [Display(Name = "Map", GroupName = "Profile (Custom)", Order = 10)]
        public bool custProfileMap
        { get; set; }
		
		/// ---
		
		[NinjaScriptProperty]
		[Display(Name = "Map Display Type", GroupName = "Profile (Custom)", Order = 11)]
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
        [Display(Name = "Footprint Bar Width", GroupName = "Misc", Order = 2)]
        public double fBarWidth
        { get; set; }
		
		/// ---
		
		[NinjaScriptProperty]
        [ReadOnly(true)]
        [Display(Name = "Footprint Bar Distance", GroupName = "Misc", Order = 3)]
        public float fBarDistance
        { get; set; }
		
		#endregion
	}
}

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

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private Infinity.FootPrintV2[] cacheFootPrintV2;
		public Infinity.FootPrintV2 FootPrintV2(int rightMargin, bool showClose, bool paintBars, bool prevProfileShow, int prevProfileWidth, bool prevProfileGradient, bool prevProfileValueArea, bool prevProfileExtendVa, bool prevProfileShowDelta, bool prevProfileBar, bool currProfileShow, int currProfileWidth, bool currProfileGradient, bool currProfileValueArea, bool currProfileExtendVa, bool currProfileShowDelta, bool currProfileBar, bool custProfileShow, int custProfileWidth, bool custProfileGradient, bool custProfileValueArea, bool custProfileExtendVa, bool custProfileShowDelta, double custProfilePctValue, int custProfileBarValue, double custProfileVolValue, double custProfileRngValue, bool custProfileMap, FPV2MapDisplayType custProfileMapType, bool showFootprint, FPV2FootprintDisplayType footprintDisplayType, bool footprintImbalances, double minImbalanceRatio, bool footprintDeltaOutline, bool footprintDeltaProfile, bool footprintDeltaGradient, bool footprintGradient, bool footprintRelativeVolume, bool showBottomArea, FPV2BottomAreaType bottomAreaType, bool bottomAreaGradient, bool showTapeStrip, int tapeStripMaxItems, double tapeStripFilter, Brush bullishColor, Brush bearishColor, Brush neutralColor, Brush profileColor, Brush mapColor, Brush pocColor, float profileMaxOpa, float mapMaxOpa, float footprintMaxOpa, FPV2Hotkeys footprintHotKey, FPV2Hotkeys mapHotKey, double rBarWidth, float rBarDistance, double fBarWidth, float fBarDistance)
		{
			return FootPrintV2(Input, rightMargin, showClose, paintBars, prevProfileShow, prevProfileWidth, prevProfileGradient, prevProfileValueArea, prevProfileExtendVa, prevProfileShowDelta, prevProfileBar, currProfileShow, currProfileWidth, currProfileGradient, currProfileValueArea, currProfileExtendVa, currProfileShowDelta, currProfileBar, custProfileShow, custProfileWidth, custProfileGradient, custProfileValueArea, custProfileExtendVa, custProfileShowDelta, custProfilePctValue, custProfileBarValue, custProfileVolValue, custProfileRngValue, custProfileMap, custProfileMapType, showFootprint, footprintDisplayType, footprintImbalances, minImbalanceRatio, footprintDeltaOutline, footprintDeltaProfile, footprintDeltaGradient, footprintGradient, footprintRelativeVolume, showBottomArea, bottomAreaType, bottomAreaGradient, showTapeStrip, tapeStripMaxItems, tapeStripFilter, bullishColor, bearishColor, neutralColor, profileColor, mapColor, pocColor, profileMaxOpa, mapMaxOpa, footprintMaxOpa, footprintHotKey, mapHotKey, rBarWidth, rBarDistance, fBarWidth, fBarDistance);
		}

		public Infinity.FootPrintV2 FootPrintV2(ISeries<double> input, int rightMargin, bool showClose, bool paintBars, bool prevProfileShow, int prevProfileWidth, bool prevProfileGradient, bool prevProfileValueArea, bool prevProfileExtendVa, bool prevProfileShowDelta, bool prevProfileBar, bool currProfileShow, int currProfileWidth, bool currProfileGradient, bool currProfileValueArea, bool currProfileExtendVa, bool currProfileShowDelta, bool currProfileBar, bool custProfileShow, int custProfileWidth, bool custProfileGradient, bool custProfileValueArea, bool custProfileExtendVa, bool custProfileShowDelta, double custProfilePctValue, int custProfileBarValue, double custProfileVolValue, double custProfileRngValue, bool custProfileMap, FPV2MapDisplayType custProfileMapType, bool showFootprint, FPV2FootprintDisplayType footprintDisplayType, bool footprintImbalances, double minImbalanceRatio, bool footprintDeltaOutline, bool footprintDeltaProfile, bool footprintDeltaGradient, bool footprintGradient, bool footprintRelativeVolume, bool showBottomArea, FPV2BottomAreaType bottomAreaType, bool bottomAreaGradient, bool showTapeStrip, int tapeStripMaxItems, double tapeStripFilter, Brush bullishColor, Brush bearishColor, Brush neutralColor, Brush profileColor, Brush mapColor, Brush pocColor, float profileMaxOpa, float mapMaxOpa, float footprintMaxOpa, FPV2Hotkeys footprintHotKey, FPV2Hotkeys mapHotKey, double rBarWidth, float rBarDistance, double fBarWidth, float fBarDistance)
		{
			if (cacheFootPrintV2 != null)
				for (int idx = 0; idx < cacheFootPrintV2.Length; idx++)
					if (cacheFootPrintV2[idx] != null && cacheFootPrintV2[idx].rightMargin == rightMargin && cacheFootPrintV2[idx].showClose == showClose && cacheFootPrintV2[idx].paintBars == paintBars && cacheFootPrintV2[idx].prevProfileShow == prevProfileShow && cacheFootPrintV2[idx].prevProfileWidth == prevProfileWidth && cacheFootPrintV2[idx].prevProfileGradient == prevProfileGradient && cacheFootPrintV2[idx].prevProfileValueArea == prevProfileValueArea && cacheFootPrintV2[idx].prevProfileExtendVa == prevProfileExtendVa && cacheFootPrintV2[idx].prevProfileShowDelta == prevProfileShowDelta && cacheFootPrintV2[idx].prevProfileBar == prevProfileBar && cacheFootPrintV2[idx].currProfileShow == currProfileShow && cacheFootPrintV2[idx].currProfileWidth == currProfileWidth && cacheFootPrintV2[idx].currProfileGradient == currProfileGradient && cacheFootPrintV2[idx].currProfileValueArea == currProfileValueArea && cacheFootPrintV2[idx].currProfileExtendVa == currProfileExtendVa && cacheFootPrintV2[idx].currProfileShowDelta == currProfileShowDelta && cacheFootPrintV2[idx].currProfileBar == currProfileBar && cacheFootPrintV2[idx].custProfileShow == custProfileShow && cacheFootPrintV2[idx].custProfileWidth == custProfileWidth && cacheFootPrintV2[idx].custProfileGradient == custProfileGradient && cacheFootPrintV2[idx].custProfileValueArea == custProfileValueArea && cacheFootPrintV2[idx].custProfileExtendVa == custProfileExtendVa && cacheFootPrintV2[idx].custProfileShowDelta == custProfileShowDelta && cacheFootPrintV2[idx].custProfilePctValue == custProfilePctValue && cacheFootPrintV2[idx].custProfileBarValue == custProfileBarValue && cacheFootPrintV2[idx].custProfileVolValue == custProfileVolValue && cacheFootPrintV2[idx].custProfileRngValue == custProfileRngValue && cacheFootPrintV2[idx].custProfileMap == custProfileMap && cacheFootPrintV2[idx].custProfileMapType == custProfileMapType && cacheFootPrintV2[idx].showFootprint == showFootprint && cacheFootPrintV2[idx].footprintDisplayType == footprintDisplayType && cacheFootPrintV2[idx].footprintImbalances == footprintImbalances && cacheFootPrintV2[idx].minImbalanceRatio == minImbalanceRatio && cacheFootPrintV2[idx].footprintDeltaOutline == footprintDeltaOutline && cacheFootPrintV2[idx].footprintDeltaProfile == footprintDeltaProfile && cacheFootPrintV2[idx].footprintDeltaGradient == footprintDeltaGradient && cacheFootPrintV2[idx].footprintGradient == footprintGradient && cacheFootPrintV2[idx].footprintRelativeVolume == footprintRelativeVolume && cacheFootPrintV2[idx].showBottomArea == showBottomArea && cacheFootPrintV2[idx].bottomAreaType == bottomAreaType && cacheFootPrintV2[idx].bottomAreaGradient == bottomAreaGradient && cacheFootPrintV2[idx].showTapeStrip == showTapeStrip && cacheFootPrintV2[idx].tapeStripMaxItems == tapeStripMaxItems && cacheFootPrintV2[idx].tapeStripFilter == tapeStripFilter && cacheFootPrintV2[idx].bullishColor == bullishColor && cacheFootPrintV2[idx].bearishColor == bearishColor && cacheFootPrintV2[idx].neutralColor == neutralColor && cacheFootPrintV2[idx].profileColor == profileColor && cacheFootPrintV2[idx].mapColor == mapColor && cacheFootPrintV2[idx].pocColor == pocColor && cacheFootPrintV2[idx].profileMaxOpa == profileMaxOpa && cacheFootPrintV2[idx].mapMaxOpa == mapMaxOpa && cacheFootPrintV2[idx].footprintMaxOpa == footprintMaxOpa && cacheFootPrintV2[idx].footprintHotKey == footprintHotKey && cacheFootPrintV2[idx].mapHotKey == mapHotKey && cacheFootPrintV2[idx].rBarWidth == rBarWidth && cacheFootPrintV2[idx].rBarDistance == rBarDistance && cacheFootPrintV2[idx].fBarWidth == fBarWidth && cacheFootPrintV2[idx].fBarDistance == fBarDistance && cacheFootPrintV2[idx].EqualsInput(input))
						return cacheFootPrintV2[idx];
			return CacheIndicator<Infinity.FootPrintV2>(new Infinity.FootPrintV2(){ rightMargin = rightMargin, showClose = showClose, paintBars = paintBars, prevProfileShow = prevProfileShow, prevProfileWidth = prevProfileWidth, prevProfileGradient = prevProfileGradient, prevProfileValueArea = prevProfileValueArea, prevProfileExtendVa = prevProfileExtendVa, prevProfileShowDelta = prevProfileShowDelta, prevProfileBar = prevProfileBar, currProfileShow = currProfileShow, currProfileWidth = currProfileWidth, currProfileGradient = currProfileGradient, currProfileValueArea = currProfileValueArea, currProfileExtendVa = currProfileExtendVa, currProfileShowDelta = currProfileShowDelta, currProfileBar = currProfileBar, custProfileShow = custProfileShow, custProfileWidth = custProfileWidth, custProfileGradient = custProfileGradient, custProfileValueArea = custProfileValueArea, custProfileExtendVa = custProfileExtendVa, custProfileShowDelta = custProfileShowDelta, custProfilePctValue = custProfilePctValue, custProfileBarValue = custProfileBarValue, custProfileVolValue = custProfileVolValue, custProfileRngValue = custProfileRngValue, custProfileMap = custProfileMap, custProfileMapType = custProfileMapType, showFootprint = showFootprint, footprintDisplayType = footprintDisplayType, footprintImbalances = footprintImbalances, minImbalanceRatio = minImbalanceRatio, footprintDeltaOutline = footprintDeltaOutline, footprintDeltaProfile = footprintDeltaProfile, footprintDeltaGradient = footprintDeltaGradient, footprintGradient = footprintGradient, footprintRelativeVolume = footprintRelativeVolume, showBottomArea = showBottomArea, bottomAreaType = bottomAreaType, bottomAreaGradient = bottomAreaGradient, showTapeStrip = showTapeStrip, tapeStripMaxItems = tapeStripMaxItems, tapeStripFilter = tapeStripFilter, bullishColor = bullishColor, bearishColor = bearishColor, neutralColor = neutralColor, profileColor = profileColor, mapColor = mapColor, pocColor = pocColor, profileMaxOpa = profileMaxOpa, mapMaxOpa = mapMaxOpa, footprintMaxOpa = footprintMaxOpa, footprintHotKey = footprintHotKey, mapHotKey = mapHotKey, rBarWidth = rBarWidth, rBarDistance = rBarDistance, fBarWidth = fBarWidth, fBarDistance = fBarDistance }, input, ref cacheFootPrintV2);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Infinity.FootPrintV2 FootPrintV2(int rightMargin, bool showClose, bool paintBars, bool prevProfileShow, int prevProfileWidth, bool prevProfileGradient, bool prevProfileValueArea, bool prevProfileExtendVa, bool prevProfileShowDelta, bool prevProfileBar, bool currProfileShow, int currProfileWidth, bool currProfileGradient, bool currProfileValueArea, bool currProfileExtendVa, bool currProfileShowDelta, bool currProfileBar, bool custProfileShow, int custProfileWidth, bool custProfileGradient, bool custProfileValueArea, bool custProfileExtendVa, bool custProfileShowDelta, double custProfilePctValue, int custProfileBarValue, double custProfileVolValue, double custProfileRngValue, bool custProfileMap, FPV2MapDisplayType custProfileMapType, bool showFootprint, FPV2FootprintDisplayType footprintDisplayType, bool footprintImbalances, double minImbalanceRatio, bool footprintDeltaOutline, bool footprintDeltaProfile, bool footprintDeltaGradient, bool footprintGradient, bool footprintRelativeVolume, bool showBottomArea, FPV2BottomAreaType bottomAreaType, bool bottomAreaGradient, bool showTapeStrip, int tapeStripMaxItems, double tapeStripFilter, Brush bullishColor, Brush bearishColor, Brush neutralColor, Brush profileColor, Brush mapColor, Brush pocColor, float profileMaxOpa, float mapMaxOpa, float footprintMaxOpa, FPV2Hotkeys footprintHotKey, FPV2Hotkeys mapHotKey, double rBarWidth, float rBarDistance, double fBarWidth, float fBarDistance)
		{
			return indicator.FootPrintV2(Input, rightMargin, showClose, paintBars, prevProfileShow, prevProfileWidth, prevProfileGradient, prevProfileValueArea, prevProfileExtendVa, prevProfileShowDelta, prevProfileBar, currProfileShow, currProfileWidth, currProfileGradient, currProfileValueArea, currProfileExtendVa, currProfileShowDelta, currProfileBar, custProfileShow, custProfileWidth, custProfileGradient, custProfileValueArea, custProfileExtendVa, custProfileShowDelta, custProfilePctValue, custProfileBarValue, custProfileVolValue, custProfileRngValue, custProfileMap, custProfileMapType, showFootprint, footprintDisplayType, footprintImbalances, minImbalanceRatio, footprintDeltaOutline, footprintDeltaProfile, footprintDeltaGradient, footprintGradient, footprintRelativeVolume, showBottomArea, bottomAreaType, bottomAreaGradient, showTapeStrip, tapeStripMaxItems, tapeStripFilter, bullishColor, bearishColor, neutralColor, profileColor, mapColor, pocColor, profileMaxOpa, mapMaxOpa, footprintMaxOpa, footprintHotKey, mapHotKey, rBarWidth, rBarDistance, fBarWidth, fBarDistance);
		}

		public Indicators.Infinity.FootPrintV2 FootPrintV2(ISeries<double> input , int rightMargin, bool showClose, bool paintBars, bool prevProfileShow, int prevProfileWidth, bool prevProfileGradient, bool prevProfileValueArea, bool prevProfileExtendVa, bool prevProfileShowDelta, bool prevProfileBar, bool currProfileShow, int currProfileWidth, bool currProfileGradient, bool currProfileValueArea, bool currProfileExtendVa, bool currProfileShowDelta, bool currProfileBar, bool custProfileShow, int custProfileWidth, bool custProfileGradient, bool custProfileValueArea, bool custProfileExtendVa, bool custProfileShowDelta, double custProfilePctValue, int custProfileBarValue, double custProfileVolValue, double custProfileRngValue, bool custProfileMap, FPV2MapDisplayType custProfileMapType, bool showFootprint, FPV2FootprintDisplayType footprintDisplayType, bool footprintImbalances, double minImbalanceRatio, bool footprintDeltaOutline, bool footprintDeltaProfile, bool footprintDeltaGradient, bool footprintGradient, bool footprintRelativeVolume, bool showBottomArea, FPV2BottomAreaType bottomAreaType, bool bottomAreaGradient, bool showTapeStrip, int tapeStripMaxItems, double tapeStripFilter, Brush bullishColor, Brush bearishColor, Brush neutralColor, Brush profileColor, Brush mapColor, Brush pocColor, float profileMaxOpa, float mapMaxOpa, float footprintMaxOpa, FPV2Hotkeys footprintHotKey, FPV2Hotkeys mapHotKey, double rBarWidth, float rBarDistance, double fBarWidth, float fBarDistance)
		{
			return indicator.FootPrintV2(input, rightMargin, showClose, paintBars, prevProfileShow, prevProfileWidth, prevProfileGradient, prevProfileValueArea, prevProfileExtendVa, prevProfileShowDelta, prevProfileBar, currProfileShow, currProfileWidth, currProfileGradient, currProfileValueArea, currProfileExtendVa, currProfileShowDelta, currProfileBar, custProfileShow, custProfileWidth, custProfileGradient, custProfileValueArea, custProfileExtendVa, custProfileShowDelta, custProfilePctValue, custProfileBarValue, custProfileVolValue, custProfileRngValue, custProfileMap, custProfileMapType, showFootprint, footprintDisplayType, footprintImbalances, minImbalanceRatio, footprintDeltaOutline, footprintDeltaProfile, footprintDeltaGradient, footprintGradient, footprintRelativeVolume, showBottomArea, bottomAreaType, bottomAreaGradient, showTapeStrip, tapeStripMaxItems, tapeStripFilter, bullishColor, bearishColor, neutralColor, profileColor, mapColor, pocColor, profileMaxOpa, mapMaxOpa, footprintMaxOpa, footprintHotKey, mapHotKey, rBarWidth, rBarDistance, fBarWidth, fBarDistance);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Infinity.FootPrintV2 FootPrintV2(int rightMargin, bool showClose, bool paintBars, bool prevProfileShow, int prevProfileWidth, bool prevProfileGradient, bool prevProfileValueArea, bool prevProfileExtendVa, bool prevProfileShowDelta, bool prevProfileBar, bool currProfileShow, int currProfileWidth, bool currProfileGradient, bool currProfileValueArea, bool currProfileExtendVa, bool currProfileShowDelta, bool currProfileBar, bool custProfileShow, int custProfileWidth, bool custProfileGradient, bool custProfileValueArea, bool custProfileExtendVa, bool custProfileShowDelta, double custProfilePctValue, int custProfileBarValue, double custProfileVolValue, double custProfileRngValue, bool custProfileMap, FPV2MapDisplayType custProfileMapType, bool showFootprint, FPV2FootprintDisplayType footprintDisplayType, bool footprintImbalances, double minImbalanceRatio, bool footprintDeltaOutline, bool footprintDeltaProfile, bool footprintDeltaGradient, bool footprintGradient, bool footprintRelativeVolume, bool showBottomArea, FPV2BottomAreaType bottomAreaType, bool bottomAreaGradient, bool showTapeStrip, int tapeStripMaxItems, double tapeStripFilter, Brush bullishColor, Brush bearishColor, Brush neutralColor, Brush profileColor, Brush mapColor, Brush pocColor, float profileMaxOpa, float mapMaxOpa, float footprintMaxOpa, FPV2Hotkeys footprintHotKey, FPV2Hotkeys mapHotKey, double rBarWidth, float rBarDistance, double fBarWidth, float fBarDistance)
		{
			return indicator.FootPrintV2(Input, rightMargin, showClose, paintBars, prevProfileShow, prevProfileWidth, prevProfileGradient, prevProfileValueArea, prevProfileExtendVa, prevProfileShowDelta, prevProfileBar, currProfileShow, currProfileWidth, currProfileGradient, currProfileValueArea, currProfileExtendVa, currProfileShowDelta, currProfileBar, custProfileShow, custProfileWidth, custProfileGradient, custProfileValueArea, custProfileExtendVa, custProfileShowDelta, custProfilePctValue, custProfileBarValue, custProfileVolValue, custProfileRngValue, custProfileMap, custProfileMapType, showFootprint, footprintDisplayType, footprintImbalances, minImbalanceRatio, footprintDeltaOutline, footprintDeltaProfile, footprintDeltaGradient, footprintGradient, footprintRelativeVolume, showBottomArea, bottomAreaType, bottomAreaGradient, showTapeStrip, tapeStripMaxItems, tapeStripFilter, bullishColor, bearishColor, neutralColor, profileColor, mapColor, pocColor, profileMaxOpa, mapMaxOpa, footprintMaxOpa, footprintHotKey, mapHotKey, rBarWidth, rBarDistance, fBarWidth, fBarDistance);
		}

		public Indicators.Infinity.FootPrintV2 FootPrintV2(ISeries<double> input , int rightMargin, bool showClose, bool paintBars, bool prevProfileShow, int prevProfileWidth, bool prevProfileGradient, bool prevProfileValueArea, bool prevProfileExtendVa, bool prevProfileShowDelta, bool prevProfileBar, bool currProfileShow, int currProfileWidth, bool currProfileGradient, bool currProfileValueArea, bool currProfileExtendVa, bool currProfileShowDelta, bool currProfileBar, bool custProfileShow, int custProfileWidth, bool custProfileGradient, bool custProfileValueArea, bool custProfileExtendVa, bool custProfileShowDelta, double custProfilePctValue, int custProfileBarValue, double custProfileVolValue, double custProfileRngValue, bool custProfileMap, FPV2MapDisplayType custProfileMapType, bool showFootprint, FPV2FootprintDisplayType footprintDisplayType, bool footprintImbalances, double minImbalanceRatio, bool footprintDeltaOutline, bool footprintDeltaProfile, bool footprintDeltaGradient, bool footprintGradient, bool footprintRelativeVolume, bool showBottomArea, FPV2BottomAreaType bottomAreaType, bool bottomAreaGradient, bool showTapeStrip, int tapeStripMaxItems, double tapeStripFilter, Brush bullishColor, Brush bearishColor, Brush neutralColor, Brush profileColor, Brush mapColor, Brush pocColor, float profileMaxOpa, float mapMaxOpa, float footprintMaxOpa, FPV2Hotkeys footprintHotKey, FPV2Hotkeys mapHotKey, double rBarWidth, float rBarDistance, double fBarWidth, float fBarDistance)
		{
			return indicator.FootPrintV2(input, rightMargin, showClose, paintBars, prevProfileShow, prevProfileWidth, prevProfileGradient, prevProfileValueArea, prevProfileExtendVa, prevProfileShowDelta, prevProfileBar, currProfileShow, currProfileWidth, currProfileGradient, currProfileValueArea, currProfileExtendVa, currProfileShowDelta, currProfileBar, custProfileShow, custProfileWidth, custProfileGradient, custProfileValueArea, custProfileExtendVa, custProfileShowDelta, custProfilePctValue, custProfileBarValue, custProfileVolValue, custProfileRngValue, custProfileMap, custProfileMapType, showFootprint, footprintDisplayType, footprintImbalances, minImbalanceRatio, footprintDeltaOutline, footprintDeltaProfile, footprintDeltaGradient, footprintGradient, footprintRelativeVolume, showBottomArea, bottomAreaType, bottomAreaGradient, showTapeStrip, tapeStripMaxItems, tapeStripFilter, bullishColor, bearishColor, neutralColor, profileColor, mapColor, pocColor, profileMaxOpa, mapMaxOpa, footprintMaxOpa, footprintHotKey, mapHotKey, rBarWidth, rBarDistance, fBarWidth, fBarDistance);
		}
	}
}

#endregion
