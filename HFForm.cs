using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace HaiFeng
{
    public partial class HFForm : Form
    {
        public HFForm()
        {
            InitializeComponent();
            this.Load += Form1_Load;
        }
        HfDataGridView dataGridViewOrder = new HfDataGridView { Name = "order" };
        HfDataGridView dataGridViewTrade = new HfDataGridView { Name = "trade" };
        HfDataGridView dataGridViewInstrument = new HfDataGridView { Name = "instrument" };
        HfDataGridView dataGridViewPosition = new HfDataGridView { Name = "position" };
        HfDataGridView dataGridViewAccount = new HfDataGridView { Name = "account" };
        HfDataGridView dataGridViewQuote = new HfDataGridView { Name = "quote" };
        HfDataGridView dataGridViewInfo = new HfDataGridView { Name = "msg" };

        private Assembly ass = Assembly.LoadFrom("Proxy.dll");
        private TradeExt _t;
        private QuoteExt _q;
        private Config _cfg;
        private BindingSource _bsOrder = new BindingSource { DataSource = new SortableBindingList<OrderField>() };
        private BindingSource _bsTrade = new BindingSource { DataSource = new SortableBindingList<TradeField>() };
        private BindingSource _bsPosi = new BindingSource { DataSource = new SortableBindingList<PositionField>() };
        internal BindingSource bsServer = new BindingSource { DataSource = new SortableBindingList<PositionField>() };
        private string _investor;
        private string _password;

        private void Form1_Load(object sender, EventArgs e)
        {
            if (File.Exists("./config.json"))
                _cfg = JsonConvert.DeserializeObject<Config>(File.ReadAllText("./config.json"));
            else
                _cfg = new Config();

            InitControlEvent();

            InitServer();

            //需加上 DataSourceUpdateMode 否则修改不会同步
            this.comboBoxServer.DataBindings.Add("Text", _cfg.Account, "ServerName", false, DataSourceUpdateMode.OnPropertyChanged);
            this.textBoxUser.DataBindings.Add("Text", _cfg.Account, "Investor", false, DataSourceUpdateMode.OnPropertyChanged);
            this.textBoxAppID.DataBindings.Add("Text", _cfg.Account, "AppID", false, DataSourceUpdateMode.OnPropertyChanged);
            this.textBoxAuthCode.DataBindings.Add("Text", _cfg.Account, "AuthCode", false, DataSourceUpdateMode.OnPropertyChanged);
            this.textBoxProductInfo.DataBindings.Add("Text", _cfg.Account, "ProductInfo", false, DataSourceUpdateMode.OnPropertyChanged);
            //会导致无法最小化 this.DataBindings.Add("WindowState", _cfg, "WindowState");
            this.WindowState = _cfg.WindowState;
            this.DataBindings.Add("Location", _cfg, "Location", false, DataSourceUpdateMode.OnPropertyChanged);
            this.DataBindings.Add("Size", _cfg, "Size", false, DataSourceUpdateMode.OnPropertyChanged);
            this.splitContainer3.DataBindings.Add("SplitterDistance", _cfg, "QuoteHeight", false, DataSourceUpdateMode.OnPropertyChanged);

            //界面配置加载
            InitGrid();

            this.textBoxPwd.Focus();
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            _cfg.WindowState = this.WindowState;

            var gss = new[] { "OrderLayer", "TradeLayer", "PosiLayer", "AccLayer", "InstLayer", "InfoLayer" };
            var dgvs = new[] { this.dataGridViewOrder, this.dataGridViewTrade, this.dataGridViewPosition, this.dataGridViewAccount, this.dataGridViewInstrument, this.dataGridViewInfo/*, this.dataGridViewQuote */};
            for (int i = 0; i < dgvs.Length; ++i)
            {
                _cfg.GetType().GetProperty(gss[i], System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public).SetValue(_cfg, dgvs[i].SaveStyle()); //直接用=赋值,不正确
            }

            File.WriteAllText("./config.json", JsonConvert.SerializeObject(_cfg, Formatting.Indented));
            File.WriteAllText("./server.json", JsonConvert.SerializeObject((this.comboBoxServer.DataSource as BindingSource).DataSource, Formatting.Indented), Encoding.GetEncoding("GB2312"));

            if (_t != null && _t.IsLogin)
                _t.ReqUserLogout();
            if (_q != null && _q.IsLogin)
                _q.ReqUserLogout();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = MessageBox.Show(this, "确认退出(Y/N)", "提醒", MessageBoxButtons.YesNo) == DialogResult.No;
            base.OnClosing(e);
        }

        private void InitControlEvent()
        {
            this.FormClosed += Form1_FormClosed;
            this.buttonLogin.Click += buttonLogin_Click;

            this.buttonCancel.Click += ButtonCancel_Click;
            this.buttonCancelAll.Click += ButtonCancel_Click;

            this.radioButtonAll.CheckedChanged += OrderFilterChange;
            this.radioButtonNormal.CheckedChanged += OrderFilterChange;
            this.radioButtonFilled.CheckedChanged += OrderFilterChange;
            this.radioButtonCancel.CheckedChanged += OrderFilterChange;
            this.radioButtonError.CheckedChanged += OrderFilterChange;


            //双击撤单/平仓
            this.dataGridViewOrder.CellDoubleClick += dataGridView_CellDoubleClick;
            this.dataGridViewPosition.CellDoubleClick += dataGridView_CellDoubleClick;

            this.comboBoxInstrument.SelectedIndexChanged += comboBoxInstrument_SelectedIndexChanged;

            this.buttonBuy.Click += this.buttonBuy_Click;
            this.buttonSell.Click += this.buttonSell_Click;
            this.labelOffset.Click += this.labelOffset_Click;
            this.labelPrice.Click += this.labelPrice_Click;


            labelOffset_Click(null, null); //自动开平
        }

        //初始化服务前置
        void InitServer()
        {
            var list = new SortableBindingList<FutureBroker>();
            if (File.Exists("./server.json"))
                list = JsonConvert.DeserializeObject<SortableBindingList<FutureBroker>>(File.ReadAllText("./server.json", Encoding.GetEncoding("GB2312")));
            else
            {
                list.Add(new FutureBroker
                {
                    Type = ProxyType.CTP,
                    Name = "模拟",
                    Broker = "9999",
                    TradeAddr = "tcp://180.168.146.187:10101",
                    QuoteAddr = "tcp://180.168.146.187:10111",
                });
            }
            bsServer = new BindingSource { DataSource = list };
            this.comboBoxServer.DataSource = bsServer;
            this.comboBoxServer.DisplayMember = "Name";
        }

        //初始化显示的表格
        void InitGrid()
        {
            this.dataGridViewInfo.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "date",
                HeaderText = "时间",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                Width = 80,
            });

            //消息时间格式
            this.dataGridViewInfo.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "msg",
                HeaderText = "消息",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            });
            this.dataGridViewInfo.Columns[0].DefaultCellStyle.Format = "T";
            this.dataGridViewInfo.Columns[1].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;

            this.panelOrder.Controls.Add(dataGridViewOrder);
            this.tabPageTrade.Controls.Add(dataGridViewTrade);
            this.tabPagePosi.Controls.Add(dataGridViewPosition);
            this.tabPageInstrument.Controls.Add(dataGridViewInstrument);
            this.tabPageInfo.Controls.Add(dataGridViewInfo);
            this.splitContainer1.Panel1.Controls.Add(dataGridViewAccount);
            this.splitContainer3.Panel1.Controls.Add(dataGridViewQuote);

            this.dataGridViewOrder.DataSource = _bsOrder;
            this.dataGridViewTrade.DataSource = _bsTrade;
            this.dataGridViewPosition.DataSource = _bsPosi;
            this.dataGridViewAccount.DataSource = new BindingSource { DataSource = new SortableBindingList<TradingAccount>() };
            this.dataGridViewInstrument.DataSource = new BindingSource { DataSource = new SortableBindingList<InstrumentField>() };
            this.dataGridViewQuote.DataSource = new BindingSource { DataSource = new SortableBindingList<MarketData>() };

            var gss = new[] { _cfg.OrderLayer, _cfg.TradeLayer, _cfg.PosiLayer, _cfg.AccLayer, _cfg.InstLayer, _cfg.InfoLayer };
            var dgvs = new[] { this.dataGridViewOrder, this.dataGridViewTrade, this.dataGridViewPosition, this.dataGridViewAccount, this.dataGridViewInstrument, this.dataGridViewInfo/*, this.dataGridViewQuote */};
            for (int i = 0; i < dgvs.Length; ++i)
            {
                var dgv = dgvs[i];
                if (dgv.Parent is TabPage)
                    dgv.Parent.Show();
                else
                    dgv.Parent.Parent.Show();
                if (gss[i] != null)
                    dgv.LoadStyle(gss[i]);
                else
                {
                    dgv.AutoResizeColumns();
                    foreach (DataGridViewColumn col in dgv.Columns)
                    {
                        col.Width += 20;
                        if (col.ValueType == typeof(double))
                            col.DefaultCellStyle.Format = "N2";
                        else if (col.ValueType == typeof(string))
                        {
                            if (col.Name == "msg")//消息内容
                                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
                            else
                                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                        }
                    }
                }
            }
        }

        //委托显示过滤
        private void OrderFilterChange(object sender, EventArgs e)
        {
            if (!(sender as RadioButton).Checked) return;
            var originalList = _t.DicOrderField.Values.ToList();
            //执行过滤
            var list = new List<OrderField>();

            if (sender == this.radioButtonAll)
                list.AddRange(originalList);
            else if (sender == this.radioButtonNormal)
                list.AddRange(originalList.Where(n => n.Status == OrderStatus.Normal || n.Status == OrderStatus.Partial));
            else if (sender == this.radioButtonFilled)
                list.AddRange(originalList.Where(n => n.Status == OrderStatus.Filled));
            else if (sender == this.radioButtonCancel)
                list.AddRange(originalList.Where(n => n.Status == OrderStatus.Canceled));
            else if (sender == this.radioButtonError)
                list.AddRange(originalList.Where(n => n.Status == OrderStatus.Error));

            _bsOrder.RaiseListChangedEvents = false;
            _bsOrder.Clear();
            foreach (var o in list)
                _bsOrder.Add(o);
            //if(_bsOrder.IsSorted)	//排序是否自动执行??? ResetBindings(false)时 Yes
            //_bsOrder.Sort
            _bsOrder.RaiseListChangedEvents = true;
            _bsOrder.ResetBindings(false);  //数据显示在gridview中

            //if (sender == this.radioButtonAll)
            //	_bsOrder.Filter = "";
            //else if (sender == this.radioButtonNormal)
            //	_bsOrder.Filter = $"Status = {(int)OrderStatus.Normal}";// AND Status = {(int)OrderStatus.Partial}";
            //else if (sender == this.radioButtonFilled)
            //	_bsOrder.Filter = $"Status = {(int)OrderStatus.Filled}";
            //else if (sender == this.radioButtonCancel)
            //	_bsOrder.Filter = $"Status = {(int)OrderStatus.Canceled}";// AND Status = {(int)OrderStatus.Error}";
            //else if (sender == this.radioButtonError)
            //	_bsOrder.Filter = $"Status = {(int)OrderStatus.Error}";// AND Status = {(int)OrderStatus.Error}";

            //(_bsOrder.DataSource as FilteredBindingList<OrderField>).ApplyFilter();
        }

        private void ButtonCancel_Click(object sender, EventArgs e)
        {
            var bs = this.dataGridViewOrder.DataSource as BindingSource;
            if (bs.Current == null) return;

            if (sender == this.buttonCancel) //撤单
            {
                _t.ReqOrderAction((bs.Current as OrderField).OrderID);
            }
            else if (sender == this.buttonCancelAll) //全撤
            {
                foreach (OrderField of in bs)
                {
                    if (of.Status == OrderStatus.Normal || of.Status == OrderStatus.Partial)
                        _t.ReqOrderAction(of.OrderID);
                }
            }
        }

        //双击撤单/平仓
        private void dataGridView_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex < 0 || e.RowIndex < 0) return;

            var rgv = (DataGridView)sender;
            var row = rgv.CurrentRow;
            if (row == null) return;

            if (rgv == this.dataGridViewOrder)
            {
                if (MessageBox.Show(this, "确认撤单操作(Y/N)", "确认", MessageBoxButtons.YesNo) == DialogResult.No) return;

                OrderField of = (OrderField)row.DataBoundItem;
                if (of.Status == OrderStatus.Normal || of.Status == OrderStatus.Partial)
                    _t.ReqOrderAction(of.OrderID);
            }
            else if (rgv == this.dataGridViewPosition)
            {
                if (MessageBox.Show(this, "确认快速平仓操作(Y/N)", "确认", MessageBoxButtons.YesNo) == DialogResult.No) return;

                PositionField pf = (PositionField)row.DataBoundItem;
                var dir = pf.Direction == DirectionType.Buy ? DirectionType.Sell : DirectionType.Buy;
                var price = dir == DirectionType.Buy ? _q.DicTick[pf.InstrumentID].AskPrice : _q.DicTick[pf.InstrumentID].BidPrice;
                var lots = pf.Position;
                if (_t.DicInstrumentField[pf.InstrumentID].ExchangeID == Exchange.SHFE && pf.TdPosition > 0)
                {
                    _t.ReqOrderInsert(pf.InstrumentID, dir, OffsetType.CloseToday, price, pf.TdPosition, 0);
                    lots -= pf.TdPosition;
                }
                if (lots > 0)
                    close(pf, price, lots);

            }
        }

        #region 下单相关的功能
        //合约选择
        private void comboBoxInstrument_SelectedIndexChanged(object sender, EventArgs e)
        {
            //价格最小变动
            this.numericUpDownPrice.Increment = (decimal)_t.DicInstrumentField[this.comboBoxInstrument.Text].PriceTick;
            this.numericUpDownPrice.DecimalPlaces = this.numericUpDownPrice.Increment >= 1 ? 0 : this.numericUpDownPrice.Increment.ToString().Split('.')[1].Length;
            this.numericUpDownPrice.Value = 0; //新合约行情来的时候才会更新
            _q.ReqSubscribeMarketData(this.comboBoxInstrument.Text);
        }

        //买入
        private void buttonBuy_Click(object sender, EventArgs e)
        {
            Order(DirectionType.Buy);
        }

        private void buttonSell_Click(object sender, EventArgs e)
        {
            Order(DirectionType.Sell);
        }

        //自动/开平切换
        private void labelOffset_Click(object sender, EventArgs e)
        {
            this.labelOffset.Text = this.comboBoxOffset.Enabled ? "自动" : "开平";
            this.comboBoxOffset.Enabled = this.labelOffset.Text == "开平";
        }

        //跟盘价/指定价
        private void labelPrice_Click(object sender, EventArgs e)
        {
            this.labelPrice.Text = this.numericUpDownPrice.Enabled ? "跟盘价" : "指定价";
            this.numericUpDownPrice.Enabled = this.labelPrice.Text == "指定价";
        }

        //发单
        void Order(DirectionType dire)
        {
            //for (int i = 0; i < 100; i++)
            {
                var isBuy = dire == DirectionType.Buy;
                var price = (double)this.numericUpDownPrice.Value;
                if (this.labelPrice.Text == "跟盘价")
                    price = double.Parse(isBuy ? this.labelAsk.Text : this.labelBid.Text);
                if (isBuy)
                    price += (double)(this.numericUpDownOffset.Value * this.numericUpDownPrice.Increment);
                else
                    price -= (double)(this.numericUpDownOffset.Value * this.numericUpDownPrice.Increment);

                if (this.comboBoxOffset.Enabled)
                {
                    _t.ReqOrderInsert(this.comboBoxInstrument.Text, dire, (OffsetType)this.comboBoxOffset.SelectedValue, price, (int)this.numericUpDownVolume.Value, 0);
                }
                else
                {//自动
                    var lots = (int)this.numericUpDownVolume.Value;
                    PositionField pf;
                    if (_t.DicPositionField.TryGetValue($"{this.comboBoxInstrument.Text}_{(isBuy ? "Sell" : "Buy")}", out pf) && pf.Position > 0)
                    {
                        //平仓
                        close(pf, price, lots);
                    }
                    else
                        _t.ReqOrderInsert(this.comboBoxInstrument.Text, dire, OffsetType.Open, price, lots, 0);
                }
            }
        }

        private int close(PositionField pf, double price, int lots)
        {
            var volClose = Math.Min(lots, pf.Position);   //可平量
            var rtn = lots - volClose;
            var dire = pf.Direction == DirectionType.Buy ? DirectionType.Sell : DirectionType.Buy;
            if (_t.DicInstrumentField[pf.InstrumentID].ExchangeID == Exchange.SHFE && pf.TdPosition > 0)
            {
                var tdClose = Math.Min(pf.TdPosition, volClose);
                _t.ReqOrderInsert(pf.InstrumentID, dire, OffsetType.CloseToday, price, tdClose, 0);
                volClose -= tdClose;
            }
            if (volClose > 0)
            {
                _t.ReqOrderInsert(pf.InstrumentID, dire, OffsetType.Close, price, volClose, 0);
            }
            return rtn;
        }
        //行情响应
        private void OnTick(object sender, TickEventArgs e)
        {
            if (!_q.IsLogin) return;
            this.BeginInvoke(new Action(() =>
            {
                var bs = this.dataGridViewQuote.DataSource as BindingSource;
                if (bs.IndexOf(e.Tick) < 0)
                    bs.Add(e.Tick);
                if ((this.labelPrice.Text == "跟盘价" || this.numericUpDownPrice.Value == 0) && e.Tick.InstrumentID == this.comboBoxInstrument.Text)
                {
                    this.labelUpper.Text = e.Tick.UpperLimitPrice.ToString();
                    this.labelLower.Text = e.Tick.LowerLimitPrice.ToString();
                    this.numericUpDownPrice.Value = (decimal)e.Tick.LastPrice;

                    this.labelAsk.Text = e.Tick.AskPrice.ToString();
                    this.labelAskVol.Text = e.Tick.AskVolume.ToString();
                    this.labelBid.Text = e.Tick.BidPrice.ToString();
                    this.labelBidVol.Text = e.Tick.BidVolume.ToString();
                }
            }));
        }
        #endregion

        //登录
        private void buttonLogin_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(this.textBoxUser.Text) || string.IsNullOrEmpty(this.textBoxPwd.Text))
                return;

            if (_t != null && _t.IsLogin) return; //已经登陆成功,返回.

            if (_t != null)
            {
                _t.ReqUserLogout();
                _t = null;
            }


            var svr = (FutureBroker)this.comboBoxServer.SelectedItem;
            _t = new TradeExt();// (Trade)Activator.CreateInstance(ass.GetType($"HaiFeng.{svr.Type}Trade"));
            _investor = this.textBoxUser.Text;
            _password = this.textBoxPwd.Text;

            _t.FrontAddr = svr.TradeAddr;
            _t.Broker = svr.Broker;
            _t.Investor = _investor;
            _t.Password = _password;
            // SE新增
            _t.ProductInfo = this.textBoxProductInfo.Text;
            _t.AppID = this.textBoxAppID.Text;
            _t.AuthCode = this.textBoxAuthCode.Text;

            _t.OnFrontConnected += (snd, ea) =>
            {
                ShowMsg($"交易连接成功");
                _t.ReqUserLogin();
            };
            _t.OnRtnPasswordUpdate += (snd, ea) => ShowMsg($"密码更新{ea.ErrorMsg}");
            _t.OnRspUserLogin += RspLogin;
            _t.OnRspUserLogout += (snd, ea) => ShowMsg($"[{_investor}]交易退出:{ea.Value}");
            _t.OnRtnOrder += OnOrder;
            _t.OnRtnCancel += OnCancel;
            _t.OnRtnTrade += OnTrade;
            _t.OnRtnErrOrder += OnErrOrder;
            _t.OnRtnErrCancel += OnErrCancel;
            _t.OnRtnExchangeStatus += (snd, ea) => ShowMsg($"[{_investor}][{ea.Exchange,8}{ea.Status}]");
            _t.OnRtnNotice += (snd, ea) => ShowMsg($"[{_investor}]提醒信息:{ea.Value}");

            ShowMsg($"登录中...");
            this.Text += $"({_t.Version.Split(' ')[0]})";
            _t.ReqConnect();
        }

        private void ShowMsg(string msg)
        {
            if (this.IsDisposed) return;

            this.BeginInvoke(new Action(() =>
            {
                this.comboBoxMsg.Items.Insert(0, $"{DateTime.Now.TimeOfDay} {msg}");
                this.comboBoxMsg.SelectedIndex = 0;
                //添加到消息表中
                this.dataGridViewInfo.Rows.Insert(0, DateTime.Now, msg);
            }));
        }

        //登录响应
        private void RspLogin(object sender, ErrorEventArgs e)
        {
            var t = (Trade)sender;
            ShowMsg($"[{_investor}]登录:{e.ErrorID}=={e.ErrorMsg}");

            if (e.ErrorID == 140)
            {
                _t.ReqUserPasswordUpdate(_t.Password, "Hf123456");
            }
            if (e.ErrorID == 0)
            {
                this.Invoke(new Action(() =>
                {
                    foreach (var posi in _t.DicPositionField.Values)
                        _bsPosi.Add(posi);
                }));
                FutureBroker svr = (this.comboBoxServer.DataSource as BindingSource).Current as FutureBroker;

                if (svr == null)
                {
                    ShowMsg("前置配置为空无法登录行情");
                    return;
                }
                if (_q != null)
                {
                    _q.ReqUserLogout();
                    _q = null;
                }
                if (svr.Type == ProxyType.Tdx)
                {
                    _q = new QuoteExt();// (Quote)Activator.CreateInstance(ass.GetType($"HaiFeng.{svr.Type}Quote"));
                                        //this.Invoke(new Action(() => this.comboBoxInstrument.Items.Add("000001")));
                }
                else
                    _q = new QuoteExt();// (Quote)Activator.CreateInstance(ass.GetType($"HaiFeng.{svr.Type}Quote"));
                _q.Broker = svr.Broker;
                _q.FrontAddr = svr.QuoteAddr;
                _q.Investor = _investor;
                _q.Password = _password;

                _q.OnFrontConnected += (snd, ea) =>
                {
                    ShowMsg($"行情连接成功");
                    _q.ReqUserLogin();
                };
                _q.OnRspUserLogin += (snd, ea) =>
                {
                    foreach (var v in _t.DicPositionField.Values)
                        ((Quote)snd).ReqSubscribeMarketData(v.InstrumentID);
                    ShowMsg($"行情登录成功");
                    LogSucceed();
                };
                _q.OnRtnTick += this.OnTick;
                _q.OnRspUserLogout += (snd, ea) => { ShowMsg($"[{_investor}]行情退出:{ea.Value}"); };
                _q.ReqConnect();
            }
        }

        private void LogSucceed()
        {
            this.Invoke(new Action(() =>
            {
                if (this.buttonLogin.BackColor == Color.LawnGreen) return;  //行情重复登录,不做处理.

                // 锁定
                this.comboBoxServer.Enabled = this.textBoxUser.Enabled = this.textBoxPwd.Enabled = false;

                //数据绑定
                this.buttonLogin.Enabled = false;
                (this.dataGridViewAccount.DataSource as BindingSource).Clear();
                (this.dataGridViewAccount.DataSource as BindingSource).Add(_t.TradingAccount);

                this.dataGridViewOrder.ReLoadData(_t.DicOrderField.Values.ToArray());
                this.dataGridViewTrade.ReLoadData(_t.DicTradeField.Values.ToArray());
                this.dataGridViewInstrument.ReLoadData(_t.DicInstrumentField.Values.ToArray());
                this.dataGridViewPosition.ReLoadData(_t.DicPositionField.Values.Where(n => n.Position > 0).ToArray());


                //合约列表
                this.comboBoxInstrument.Items.AddRange(_t.DicInstrumentField.Keys.ToArray());
                this.comboBoxInstrument.Sorted = true;
                //开平
                this.comboBoxOffset.DataSource = typeof(OffsetType).ToExtendedList<int>();
                this.comboBoxOffset.DisplayMember = "Value";
                this.comboBoxOffset.ValueMember = "NumericKey";

                //定制功能处理
                //LogFinish();
            }));
        }

        private void OnErrCancel(object sender, ErrOrderArgs e)
        {
        }

        private void OnErrOrder(object sender, ErrOrderArgs e)
        {
            this.Invoke(new Action(() =>
            {
                if (_bsOrder.IndexOf(e.Value) < 0)
                    _bsOrder.Add(e.Value);
            }));
        }

        private void OnTrade(object sender, TradeArgs e)
        {
            var t = (Trade)sender;
            this.Invoke(new Action(() =>
            {
                _bsTrade.Add(e.Value);

                var dirPosi = e.Value.Direction;
                if (e.Value.Offset != OffsetType.Open)
                    dirPosi = dirPosi == DirectionType.Buy ? DirectionType.Sell : DirectionType.Buy;
                var posi = _t.DicPositionField[e.Value.InstrumentID + "_" + dirPosi];

                if (_bsPosi.IndexOf(posi) < 0)  //持仓0过滤
                    _bsPosi.Add(posi);
                if (posi.Position <= 0)//条件:Position>0
                    _bsPosi.Remove(posi);

                //测试一下:filterBindingList只在增减时才会执行
                //if (_bsPosi.IndexOf(posi) < 0)
                //	_bsPosi.Add(posi);

                //var list = _bsPosi.DataSource as SortableBindingList<PositionField>;
                //var p = list.FirstOrDefault(n => n.InstrumentID == e.Value.InstrumentID && n.Direction == dirPosi);
                //if (p == null)
                //	_bsPosi.Add(_t.DicPositionField[e.Value.InstrumentID + "_" + dirPosi]);
                //else if (p.Position == 0)
                //	_bsPosi.Remove(p);
            }));
        }

        private void OnCancel(object sender, OrderArgs e)
        {
        }

        private void OnOrder(object sender, OrderArgs e)
        {
            this.Invoke(new Action(() =>
            {
                if (_bsOrder.IndexOf(e.Value) < 0)
                    _bsOrder.Add(e.Value);
            }));
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            if(this.textBoxNewPwd.Text != this.textBoxNewPwdConfirm.Text)
            {
                MessageBox.Show("两次输入的密码不一致!", "提醒", MessageBoxButtons.OK);
            }
            else
            {
                _t.ReqUserPasswordUpdate(this._password, this.textBoxNewPwd.Text);
            }
        }
    }

    internal class Config
    {
        public Account Account { get; set; } = new Account();

        public FormWindowState WindowState { get; set; } = FormWindowState.Normal;

        public Point Location { get; set; } = new Point(100, 100);

        public Size Size { get; set; } = new Size(1200, 800);

        public int QuoteHeight { get; set; } = 120;


        public GridStyle OrderLayer { get; set; } = null;
        public GridStyle TradeLayer { get; set; } = null;
        public GridStyle PosiLayer { get; set; } = null;
        public GridStyle AccLayer { get; set; } = null;
        public GridStyle QuoteLayer { get; set; } = null;
        public GridStyle InstLayer { get; set; } = null;
        public GridStyle InfoLayer { get; set; } = null;
    }
}
