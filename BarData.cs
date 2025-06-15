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
	public static class BarDataGlobals
	{
		public static double tsv = 0.0;
	}
	#endregion

	#region CategoryOrder
	[Gui.CategoryOrder("Properties", 1)]
	[Gui.CategoryOrder("Custom Profile Properties", 2)]
	#endregion

	public class BarData : Indicator
	{
		// ------------------- Internal helpers ----------------------
		// Use constants for magic numbers
		private const double DefaultValueAreaRatio = 0.682;
		private const int MaxValueAreaIterations = 1000;
		private const int MaxPrevIndexSearch = 50;
		// -----------------------------------------------------------

		#region RowItem
		public class RowItem
		{
			public double vol = 0.0;
			public double ask = 0.0;
			public double bid = 0.0;
			public double dta = 0.0;

			public RowItem() { }

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
			public int idx = 0;
			public bool ifb = false;
			public bool clc = true;
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

				if (ifb)
					cdo = 0.0;
			}

			/// --- Add methods ---
			public void addAsk(double prc, double vol, bool showPrevProfile, bool showCurrProfile, bool showCustProfile)
			{
				AddVolumeCommon(prc, vol);
				this.ask += vol;
				this.dtc = this.ask - this.bid;
				UpdateDeltaExtremes();

				UpdateProfiles(prc, vol, showPrevProfile, showCurrProfile, showCustProfile, (profile, p, v) => profile.addAsk(p, v));
				this.rowItems.GetOrAdd(prc, new RowItem()).addAsk(vol);
			}

			public void addBid(double prc, double vol, bool showPrevProfile, bool showCurrProfile, bool showCustProfile)
			{
				AddVolumeCommon(prc, vol);
				this.bid += vol;
				this.dtc = this.ask - this.bid;
				UpdateDeltaExtremes();

				UpdateProfiles(prc, vol, showPrevProfile, showCurrProfile, showCustProfile, (profile, p, v) => profile.addBid(p, v));
				this.rowItems.GetOrAdd(prc, new RowItem()).addBid(vol);
			}

			public void addVol(double prc, double vol, bool showPrevProfile, bool showCurrProfile, bool showCustProfile)
			{
				AddVolumeCommon(prc, vol);

				UpdateProfiles(prc, vol, showPrevProfile, showCurrProfile, showCustProfile, (profile, p, v) => profile.addVol(p, v));
				this.rowItems.GetOrAdd(prc, new RowItem()).addVol(vol);
			}

			// Helper for volume handling and updating min/max/range/opn/cls
			private void AddVolumeCommon(double prc, double vol)
			{
				this.vol += vol;
				this.min = (prc < this.min) ? prc : this.min;
				this.max = (prc > this.max) ? prc : this.max;
				this.rng = this.max - this.min;
				this.opn = (this.opn == 0.0) ? prc : this.opn;
				this.cls = prc;
			}

			// Helper for updating min/max delta, cdo/cdh/cdl/cdc, etc.
			private void UpdateDeltaExtremes()
			{
				this.dtl = (this.dtc < this.dtl) ? dtc : dtl;
				this.dth = (this.dtc > this.dth) ? dtc : dth;
				this.cdc = this.cdo + this.dtc;
				this.cdl = (this.cdo + this.dtl < this.cdl) ? this.cdo + this.dtl : this.cdl;
				this.cdh = (this.cdo + this.dth > this.cdh) ? this.cdo + this.dth : this.cdh;
			}

			// Helper for updating profiles
			private void UpdateProfiles(double prc, double vol, bool showPrevProfile, bool showCurrProfile, bool showCustProfile, Action<Profile, double, double> profileAction)
			{
				if (showCustProfile)
					profileAction(this.custProfile, prc, vol);
				if (showCurrProfile || showPrevProfile)
					profileAction(this.currProfile, prc, vol);
			}

			/// --- Calculations ---
			public void calc()
			{
				setAvg();
				setPoc();
			}

			public void setAvg()
			{
				if (!this.rowItems.IsEmpty)
					this.avg = this.vol / this.rowItems.Count;
			}

			public void setPoc()
			{
				if (!this.rowItems.IsEmpty)
					this.poc = this.rowItems.Aggregate((l, r) => l.Value.vol > r.Value.vol ? l : r).Key;
			}

			public double getMaxVol()
			{
				double mv = 0.0;
				foreach (var ri in this.rowItems.Values)
				{
					if (ri.ask > mv) mv = ri.ask;
					if (ri.bid > mv) mv = ri.bid;
				}
				return mv;
			}

			public double getMaxDta()
			{
				double md = 0.0;
				foreach (var ri in this.rowItems.Values)
				{
					var absDta = Math.Abs(ri.dta);
					if (absDta > md) md = absDta;
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
			public int bar = 0;
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

			/// --- Add methods ---
			public void addAsk(double prc, double vol)
			{
				AddVolumeCommon(prc, vol);
				this.ask += vol;
				this.dta = this.ask - this.bid;
				this.rowItems.GetOrAdd(prc, new RowItem()).addAsk(vol);
			}

			public void addBid(double prc, double vol)
			{
				AddVolumeCommon(prc, vol);
				this.bid += vol;
				this.dta = this.ask - this.bid;
				this.rowItems.GetOrAdd(prc, new RowItem()).addBid(vol);
			}

			public void addVol(double prc, double vol)
			{
				AddVolumeCommon(prc, vol);
				this.rowItems.GetOrAdd(prc, new RowItem()).addVol(vol);
			}

			private void AddVolumeCommon(double prc, double vol)
			{
				this.vol += vol;
				this.min = (prc < this.min) ? prc : this.min;
				this.max = (prc > this.max) ? prc : this.max;
				this.rng = this.max - this.min;
				this.opn = (this.opn == 0.0) ? prc : this.opn;
				this.cls = prc;
			}

			/// --- Calculations ---
			public void calc()
			{
				setAvg();
				setPoc();
				setValueArea();
			}

			public void setAvg()
			{
				if (!this.rowItems.IsEmpty)
					this.avg = this.vol / this.rowItems.Count;
			}

			public void setPoc()
			{
				if (!this.rowItems.IsEmpty)
					this.poc = this.rowItems.Aggregate((l, r) => l.Value.vol > r.Value.vol ? l : r).Key;
			}

			public void setValueArea()
			{
				double vah = this.poc;
				double val = this.poc;

				if (!this.rowItems.IsEmpty)
				{
					int iteCnt = 0;
					double maxPrc = this.max;
					double minPrc = this.min;
					double maxVol = this.vol * DefaultValueAreaRatio;
					double tmpVol = getVolume(this.poc);
					double upperP = this.poc;
					double lowerP = this.poc;
					double upperV = 0.0, lowerV = 0.0;

					while (tmpVol < maxVol)
					{
						if ((upperP >= maxPrc && lowerP <= minPrc) || iteCnt++ >= MaxValueAreaIterations) break;

						upperV = (upperP < maxPrc) ? getVolume(upperP + BarDataGlobals.tsv) : -1.0;
						lowerV = (lowerP > minPrc) ? getVolume(lowerP - BarDataGlobals.tsv) : -1.0;

						if (upperV > lowerV)
						{
							vah = upperP + BarDataGlobals.tsv;
							tmpVol += upperV;
							upperP = vah;
						}
						else
						{
							val = lowerP - BarDataGlobals.tsv;
							tmpVol += lowerV;
							lowerP = val;
						}
					}
				}

				this.vah = vah;
				this.val = val;
			}

			private double getVolume(double prc) =>
				this.rowItems.TryGetValue(prc, out var row) ? row.vol : 0.0;

			public double getMaxDta()
			{
				double md = 0.0;
				foreach (var ri in this.rowItems.Values)
				{
					var absDta = Math.Abs(ri.dta);
					if (absDta > md) md = absDta;
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

				foreach (var ri in this.rowItems)
					clone.rowItems.TryAdd(ri.Key, new RowItem(ri.Value.vol, ri.Value.ask, ri.Value.bid));
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
				return this.rowItems.TryGetValue(prc, out var ri) ? (ri.ask - ri.bid) : 0.0;
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

		#region Variables
		private bool log = true;
		private bool rdy = false;

		private double _vol, _ask, _bid, _cls;
		private BarItem _currBarItem;
		private Series<BarItem> barItems;
		#endregion

		#region OnStateChange
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description = @"";
				Name = "BarData";
				Calculate = Calculate.OnEachTick;
				IsOverlay = true;
				DisplayInDataBox = true;
				DrawOnPricePanel = false;
				DrawHorizontalGridLines = false;
				DrawVerticalGridLines = false;
				PaintPriceMarkers = false;
				ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
				IsSuspendedWhileInactive = false;
				MaximumBarsLookBack = MaximumBarsLookBack.Infinite;

				// ---
				calcPrevProfile = true;
				calcCurrProfile = true;
				calcCustProfile = true;

				custProfilePctValue = 1;
				custProfileBarValue = 8;
				custProfileVolValue = 1;
				custProfileRngValue = 1;
			}
			else if (State == State.Configure)
			{
				AddDataSeries(Data.BarsPeriodType.Tick, 1);
			}
			else if (State == State.DataLoaded)
			{
				barItems = new Series<BarItem>(BarsArray[0], MaximumBarsLookBack.Infinite);
				BarDataGlobals.tsv = TickSize;

				if (log)
					ClearOutputWindow();
			}
		}
		#endregion

		#region OnMarketData
		protected override void OnMarketData(MarketDataEventArgs marketDataUpdate)
		{
			if (!rdy || !Bars.IsTickReplay || barItems[0] == null || CurrentBars[0] == null)
				return;

			try
			{
				if (marketDataUpdate.MarketDataType == MarketDataType.Last)
				{
					double prc = Instrument.MasterInstrument.RoundToTickSize(marketDataUpdate.Price);
					double ask = Instrument.MasterInstrument.RoundToTickSize(marketDataUpdate.Ask);
					double bid = Instrument.MasterInstrument.RoundToTickSize(marketDataUpdate.Bid);
					double vol = marketDataUpdate.Volume;

					if (prc >= ask)
						barItems[0].addAsk(prc, vol, calcPrevProfile, calcCurrProfile, calcCustProfile);
					else if (prc <= bid)
						barItems[0].addBid(prc, vol, calcPrevProfile, calcCurrProfile, calcCustProfile);
					else
						barItems[0].addVol(prc, vol, calcPrevProfile, calcCurrProfile, calcCustProfile);

					if (State == State.Historical && barItems[0].clc == true && barItems[1] != null)
					{
						barItems[1].calc();
						if (calcCustProfile) barItems[1].custProfile.calc();
						if (calcCurrProfile || calcPrevProfile) barItems[1].currProfile.calc();
						barItems[0].clc = false;
					}
					if (State == State.Realtime)
					{
						barItems[0].calc();
						if (calcCustProfile) barItems[0].custProfile.calc();
						if (calcCurrProfile || calcPrevProfile) barItems[0].currProfile.calc();
					}
				}
			}
			catch (Exception exception)
			{
				if (log) Print(exception.ToString());
			}
		}
		#endregion

		#region OnBarUpdate
		protected override void OnBarUpdate()
		{
			try
			{
				if (CurrentBars[0] < 1 || CurrentBars[1] < 1) return;

				if (Calculate != Calculate.OnEachTick)
				{
					Draw.TextFixed(this, "calculateMessage", "Please set Calculate to 'On each tick' ...", TextPosition.Center);
					return;
				}

				try
				{
					#region tick replay
					if (Bars.IsTickReplay)
					{
						rdy = true;

						if (barItems[0] == null)
						{
							bool isFirstBarOfSession = Bars.IsFirstBarOfSession;
							barItems[0] = new BarItem(CurrentBar, isFirstBarOfSession);

							if (barItems[1] != null)
							{
								barItems[0].avg = barItems[1].avg;
								barItems[1].cls = Close[1];

								if (calcCustProfile) barItems[1].custProfile.cls = Close[1];
								if (calcCurrProfile || calcPrevProfile) barItems[1].currProfile.cls = Close[1];
							}

							barItems[0].opn = Open[0];
							barItems[0].cls = Close[0];

							if (calcCustProfile)
							{
								barItems[0].custProfile.opn = Open[0];
								barItems[0].custProfile.cls = Close[0];
							}
							if (calcCurrProfile || calcPrevProfile)
							{
								barItems[0].currProfile.opn = Open[0];
								barItems[0].currProfile.cls = Close[0];
							}

							if (isFirstBarOfSession)
							{
								if (calcCurrProfile || calcPrevProfile)
									barItems[0].currProfile.bar = CurrentBar;

								if (calcPrevProfile && barItems[1] != null)
								{
									barItems[0].prevProfile = barItems[1].currProfile.Clone();
									barItems[0].prevProfile.calc();
								}
							}
							else
							{
								if (barItems[1] != null)
								{
									barItems[0].cdo = barItems[1].cdc;
									barItems[0].cdl = barItems[1].cdc;
									barItems[0].cdh = barItems[1].cdc;
									barItems[0].cdc = barItems[1].cdc;
									if (calcCurrProfile || calcPrevProfile)
										barItems[0].currProfile = barItems[1].currProfile.Clone();
									if (calcPrevProfile)
										barItems[0].prevProfile = barItems[1].prevProfile;
								}
								if (calcCustProfile)
									initCustomProfile(CurrentBar);
							}
						}
					}
					#endregion

					#region no tick replay
					if (!Bars.IsTickReplay)
					{
						#region realtime
						if (State == State.Realtime)
						{
							if (barItems[0] == null)
							{
								barItems[0] = new BarItem(CurrentBars[0], BarsArray[0].IsFirstBarOfSession);
								initCustomProfile(CurrentBars[0]);

								if (calcCurrProfile || calcPrevProfile)
								{
									if (BarsArray[0].IsFirstBarOfSession)
									{
										if (barItems[1] != null && calcPrevProfile)
										{
											barItems[0].prevProfile = barItems[1].currProfile.Clone();
											barItems[0].prevProfile.calc();
										}
										barItems[0].currProfile.bar = CurrentBars[0];
									}
									else
									{
										int prevIndex = 1;
										for (var i = 1; i < MaxPrevIndexSearch; i++)
										{
											if (barItems[i] != null)
											{
												prevIndex = i;
												break;
											}
										}
										if (barItems[prevIndex] != null)
										{
											barItems[0].cdo = barItems[prevIndex].cdc;
											barItems[0].cdl = barItems[prevIndex].cdc;
											barItems[0].cdh = barItems[prevIndex].cdc;
											barItems[0].cdc = barItems[prevIndex].cdc;
											barItems[0].currProfile = barItems[prevIndex].currProfile.Clone();
											barItems[0].currProfile.calc();
											if (calcPrevProfile)
												barItems[0].prevProfile = barItems[prevIndex].prevProfile.Clone();
										}
									}
								}

								if (barItems[1] != null)
								{
									barItems[1].calc();
									if (calcCustProfile) barItems[1].custProfile.calc();
									if (calcCurrProfile || calcPrevProfile) barItems[1].currProfile.calc();
								}
							}

							if (BarsInProgress == 1)
							{
								_vol = Bars.GetVolume(CurrentBar);
								_ask = Bars.GetAsk(CurrentBar);
								_bid = Bars.GetBid(CurrentBar);
								_cls = Bars.GetClose(CurrentBar);

								if (_cls >= _ask)
									barItems[0].addAsk(_cls, _vol, calcPrevProfile, calcCurrProfile, calcCustProfile);
								else if (_cls <= _bid)
									barItems[0].addBid(_cls, _vol, calcPrevProfile, calcCurrProfile, calcCustProfile);
								else
									barItems[0].addVol(_cls, _vol, calcPrevProfile, calcCurrProfile, calcCustProfile);

								barItems[0].calc();
								if (calcCustProfile) barItems[0].custProfile.calc();
								if (calcCurrProfile || calcPrevProfile) barItems[0].currProfile.calc();
							}
						}
						#endregion

						#region historical
						if (State == State.Historical)
						{
							if (BarsInProgress == 0)
							{
								if (_currBarItem != null)
								{
									barItems[0] = _currBarItem;
									initCustomProfile(CurrentBars[0]);
									barItems[0].calc();
									if (calcCustProfile) barItems[0].custProfile.calc();
									if (calcCurrProfile || calcPrevProfile) barItems[0].currProfile.calc();
									_currBarItem = null;
								}
							}
							if (BarsInProgress == 1)
							{
								if (_currBarItem == null)
								{
									_currBarItem = new BarItem(CurrentBars[0], BarsArray[0].IsLastBarOfSession);

									if (calcCurrProfile || calcPrevProfile)
									{
										if (BarsArray[0].IsLastBarOfSession)
										{
											if (barItems[0] != null && calcPrevProfile)
											{
												_currBarItem.prevProfile = barItems[0].currProfile.Clone();
												_currBarItem.prevProfile.calc();
											}
											_currBarItem.currProfile.bar = CurrentBars[0];
										}
										else
										{
											int prevIndex = 0;
											for (var i = 0; i < MaxPrevIndexSearch; i++)
											{
												if (CurrentBars[0] - i >= 0 && barItems[i] != null)
												{
													prevIndex = i;
													break;
												}
											}
											if (barItems[prevIndex] != null)
											{
												_currBarItem.cdo = barItems[prevIndex].cdc;
												_currBarItem.cdl = barItems[prevIndex].cdc;
												_currBarItem.cdh = barItems[prevIndex].cdc;
												_currBarItem.cdc = barItems[prevIndex].cdc;
												_currBarItem.currProfile = barItems[prevIndex].currProfile.Clone();
												_currBarItem.currProfile.calc();
												if (calcPrevProfile)
													_currBarItem.prevProfile = barItems[prevIndex].prevProfile.Clone();
											}
										}
									}
								}

								_vol = Bars.GetVolume(CurrentBar);
								_ask = Bars.GetAsk(CurrentBar);
								_bid = Bars.GetBid(CurrentBar);
								_cls = Bars.GetClose(CurrentBar);

								if (_cls >= _ask)
									_currBarItem.addAsk(_cls, _vol, calcPrevProfile, calcCurrProfile, calcCustProfile);
								else if (_cls <= _bid)
									_currBarItem.addBid(_cls, _vol, calcPrevProfile, calcCurrProfile, calcCustProfile);
								else
									_currBarItem.addVol(_cls, _vol, calcPrevProfile, calcCurrProfile, calcCustProfile);
							}
						}
						#endregion
					}
					#endregion
				}
				catch (Exception exception)
				{
					if (log) Print("collect data - " + exception.ToString());
				}
			}
			catch (Exception exception)
			{
				if (log) NinjaTrader.Code.Output.Process(exception.ToString(), PrintTo.OutputTab1);
			}
		}
		#endregion

		#region initCustomProfile
		private void initCustomProfile(int bar)
		{
			try
			{
				if (barItems[0] == null) return;

				barItems[0].custProfile.Reset();

				int idx = bar;
				int cnt = 0;
				double vol = 0.0;
				double min = double.MaxValue;
				double max = double.MinValue;
				double rng = 0.0;
				double pct = barItems[0].currProfile.vol * (custProfilePctValue / 100.0);

				while (idx > 1)
				{
					if (barItems.IsValidDataPointAt(idx))
					{
						BarItem barItem = barItems.GetValueAt(idx);
						if (barItem != null)
						{
							var custProfile = barItems[0].custProfile;
							custProfile.min = Math.Min(barItem.min, custProfile.min);
							custProfile.max = Math.Max(barItem.max, custProfile.max);
							custProfile.rng = custProfile.max - custProfile.min;
							custProfile.opn = barItem.opn;
							custProfile.cls = (custProfile.cls == 0.0) ? barItem.cls : custProfile.cls;
							custProfile.vol += barItem.vol;
							custProfile.ask += barItem.ask;
							custProfile.bid += barItem.bid;
							custProfile.dta += barItem.dtc;

							foreach (var ri in barItem.rowItems)
							{
								custProfile.rowItems.GetOrAdd(ri.Key, new RowItem());
								custProfile.rowItems[ri.Key].vol += ri.Value.vol;
								custProfile.rowItems[ri.Key].ask += ri.Value.ask;
								custProfile.rowItems[ri.Key].bid += ri.Value.bid;
								custProfile.rowItems[ri.Key].dta += ri.Value.dta;
							}

							vol += barItem.vol;
							min = Math.Min(barItem.min, min);
							max = Math.Max(barItem.max, max);
							rng = (max - min) / TickSize;
							cnt++;

							if ((vol >= pct && cnt >= custProfileBarValue && vol >= custProfileVolValue && rng >= custProfileRngValue) || barItem.ifb)
							{
								custProfile.bar = idx;
								break;
							}
						}
					}
					idx--;
				}
				barItems[0].custProfile.calc();
			}
			catch (Exception exception)
			{
				if (log) NinjaTrader.Code.Output.Process(exception.ToString(), PrintTo.OutputTab1);
			}
		}
		#endregion

		#region Properties
		[Browsable(false)]
		[XmlIgnore]
		public Series<BarItem> BarItems => barItems;

		[NinjaScriptProperty]
		[Display(Name = "Calculate Previous Profile", GroupName = "Properties", Order = 0)]
		public bool calcPrevProfile { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Calculate Current Profile", GroupName = "Properties", Order = 1)]
		public bool calcCurrProfile { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Calculate Custom Profile", GroupName = "Properties", Order = 2)]
		public bool calcCustProfile { get; set; }

		[NinjaScriptProperty]
		[Range(0.0, 100.0)]
		[Display(Name = "Percent Value", GroupName = "Custom Profile Properties", Order = 0)]
		public double custProfilePctValue { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name = "Bar Value", GroupName = "Custom Profile Properties", Order = 1)]
		public int custProfileBarValue { get; set; }

		[NinjaScriptProperty]
		[Range(1.0, double.MaxValue)]
		[Display(Name = "Volume Value", GroupName = "Custom Profile Properties", Order = 2)]
		public double custProfileVolValue { get; set; }

		[NinjaScriptProperty]
		[Range(1.0, double.MaxValue)]
		[Display(Name = "Range Value (Ticks)", GroupName = "Custom Profile Properties", Order = 3)]
		public double custProfileRngValue { get; set; }
		#endregion
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private Infinity.BarData[] cacheBarData;
		public Infinity.BarData BarData(bool calcPrevProfile, bool calcCurrProfile, bool calcCustProfile, double custProfilePctValue, int custProfileBarValue, double custProfileVolValue, double custProfileRngValue)
		{
			return BarData(Input, calcPrevProfile, calcCurrProfile, calcCustProfile, custProfilePctValue, custProfileBarValue, custProfileVolValue, custProfileRngValue);
		}

		public Infinity.BarData BarData(ISeries<double> input, bool calcPrevProfile, bool calcCurrProfile, bool calcCustProfile, double custProfilePctValue, int custProfileBarValue, double custProfileVolValue, double custProfileRngValue)
		{
			if (cacheBarData != null)
				for (int idx = 0; idx < cacheBarData.Length; idx++)
					if (cacheBarData[idx] != null && cacheBarData[idx].calcPrevProfile == calcPrevProfile && cacheBarData[idx].calcCurrProfile == calcCurrProfile && cacheBarData[idx].calcCustProfile == calcCustProfile && cacheBarData[idx].custProfilePctValue == custProfilePctValue && cacheBarData[idx].custProfileBarValue == custProfileBarValue && cacheBarData[idx].custProfileVolValue == custProfileVolValue && cacheBarData[idx].custProfileRngValue == custProfileRngValue && cacheBarData[idx].EqualsInput(input))
						return cacheBarData[idx];
			return CacheIndicator<Infinity.BarData>(new Infinity.BarData() { calcPrevProfile = calcPrevProfile, calcCurrProfile = calcCurrProfile, calcCustProfile = calcCustProfile, custProfilePctValue = custProfilePctValue, custProfileBarValue = custProfileBarValue, custProfileVolValue = custProfileVolValue, custProfileRngValue = custProfileRngValue }, input, ref cacheBarData);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Infinity.BarData BarData(bool calcPrevProfile, bool calcCurrProfile, bool calcCustProfile, double custProfilePctValue, int custProfileBarValue, double custProfileVolValue, double custProfileRngValue)
		{
			return indicator.BarData(Input, calcPrevProfile, calcCurrProfile, calcCustProfile, custProfilePctValue, custProfileBarValue, custProfileVolValue, custProfileRngValue);
		}

		public Indicators.Infinity.BarData BarData(ISeries<double> input, bool calcPrevProfile, bool calcCurrProfile, bool calcCustProfile, double custProfilePctValue, int custProfileBarValue, double custProfileVolValue, double custProfileRngValue)
		{
			return indicator.BarData(input, calcPrevProfile, calcCurrProfile, calcCustProfile, custProfilePctValue, custProfileBarValue, custProfileVolValue, custProfileRngValue);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Infinity.BarData BarData(bool calcPrevProfile, bool calcCurrProfile, bool calcCustProfile, double custProfilePctValue, int custProfileBarValue, double custProfileVolValue, double custProfileRngValue)
		{
			return indicator.BarData(Input, calcPrevProfile, calcCurrProfile, calcCustProfile, custProfilePctValue, custProfileBarValue, custProfileVolValue, custProfileRngValue);
		}

		public Indicators.Infinity.BarData BarData(ISeries<double> input, bool calcPrevProfile, bool calcCurrProfile, bool calcCustProfile, double custProfilePctValue, int custProfileBarValue, double custProfileVolValue, double custProfileRngValue)
		{
			return indicator.BarData(input, calcPrevProfile, calcCurrProfile, calcCustProfile, custProfilePctValue, custProfileBarValue, custProfileVolValue, custProfileRngValue);
		}
	}
}

#endregion
