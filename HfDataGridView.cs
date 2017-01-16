using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;

namespace HaiFeng
{
	public class HfDataGridView : DataGridView
	{
		public HfDataGridView()
		{
			SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
			AllowUserToAddRows = false;
			AllowUserToDeleteRows = false;
			AllowUserToOrderColumns = true;

			Dock = System.Windows.Forms.DockStyle.Fill;
			Location = new System.Drawing.Point(3, 3);

			ReadOnly = true;
			RowHeadersWidth = 10;

			RowsDefaultCellStyle.BackColor = System.Drawing.Color.WhiteSmoke;

			RowTemplate.Height = 23;
			SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;

			ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
			ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
			//ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

			AlternatingRowsDefaultCellStyle.BackColor = System.Drawing.Color.White;

			DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
			DefaultCellStyle.SelectionBackColor = System.Drawing.Color.LightSteelBlue;
			DefaultCellStyle.SelectionForeColor = System.Drawing.Color.Black;

		}

		protected override void OnColumnAdded(DataGridViewColumnEventArgs e)
		{
			e.Column.SortMode = DataGridViewColumnSortMode.NotSortable;
			base.OnColumnAdded(e);
		}

		//protected override void OnDataSourceChanged(EventArgs e)
		//{
		//	if (!string.IsNullOrEmpty(this.SortColumnName))
		//	{
		//		this.Columns[this.SortColumnName].SortMode = DataGridViewColumnSortMode.Automatic;
		//		this.Sort(this.Columns[this.SortColumnName], this.SortOrder == SortOrder.Ascending ? ListSortDirection.Ascending : ListSortDirection.Descending);
		//	}
		//	base.OnDataSourceChanged(e);
		//}

		//绑定结束:去掉排序图标,标题正中显示正常
		//protected override void OnDataBindingComplete(DataGridViewBindingCompleteEventArgs e)
		//{
		//	//每次排序后均会触发******************
		//	//this.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);
		//	//foreach (DataGridViewColumn col in this.Columns)
		//	//col.Width += 20;	//增加排序的图标的空间
		//	//导致无法排序 
		//	//col.SortMode = DataGridViewColumnSortMode.NotSortable;
		//	base.OnDataBindingComplete(e);
		//}

		//处理单点列标题
		protected override void OnCellClick(DataGridViewCellEventArgs e)
		{
			//左上角点击:全选
			if (e.ColumnIndex < 0 && e.RowIndex < 0)
			{
				foreach (DataGridViewRow row in this.Rows)
					row.Selected = true;
				return;
			}
			if (e.ColumnIndex >= 0 && e.RowIndex < 0)
			{
				//单击标题进行排序
				if (this.SortedColumn == null)
				{//对不支持排序的绑定执行时会报错
					this.Sort(this.Columns[e.ColumnIndex], ListSortDirection.Ascending);
					this.Columns[e.ColumnIndex].SortMode = DataGridViewColumnSortMode.Automatic;
				}
				else if (this.Columns.IndexOf(this.SortedColumn) != e.ColumnIndex)
				{
					this.SortedColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
					this.Columns[e.ColumnIndex].SortMode = DataGridViewColumnSortMode.Automatic;
				}
				return;
			}
			base.OnCellClick(e);
		}

		//行增加: 排序
		protected override void OnRowsAdded(DataGridViewRowsAddedEventArgs e)
		{
			base.OnRowsAdded(e);
			if (SortOrder == SortOrder.None) return;
			Sort(SortedColumn, SortOrder == SortOrder.Ascending ? ListSortDirection.Ascending : ListSortDirection.Descending);
		}

		protected override void OnDataError(bool displayErrorDialogIfNoHandler, DataGridViewDataErrorEventArgs e)
		{
			e.Cancel = true;
			//base.OnDataError(displayErrorDialogIfNoHandler, e);
		}

		List<string> listName = new List<string>(new[] { "Direction", "Offset", "ExchangeID", "Status", "Hedge" });
		List<Type> listType = new List<Type>(new[] { typeof(DirectionType), typeof(OffsetType), typeof(Exchange), typeof(OrderStatus), typeof(HedgeType) });

		protected override void OnCellFormatting(DataGridViewCellFormattingEventArgs e)
		{
			if (e.ColumnIndex < 0 || e.RowIndex < 0) return;

			if (e.Value is string)
			{
				e.Value = (e.Value as string).Trim();
				return;
			}

			var col = Columns[e.ColumnIndex];

			var idx = listName.IndexOf(col.Name);
			if (idx < 0) return;

			e.Value = ((KeyValueTriplet<Enum, int, string>)listType[idx].ToExtendedList<int>()[(int)e.Value]).Value;
			base.OnCellFormatting(e);
		}

		public GridStyle SaveStyle()
		{
			var style = new GridStyle();
			foreach (DataGridViewColumn col in this.Columns)
			{
				var colStyle = new ColumnStyle();
				foreach (var p in typeof(ColumnStyle).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
					if (p.Name == "Format")
						colStyle.Format = col.DefaultCellStyle.Format;
					else if (p.Name == "ColumnType")
						colStyle.ColumnType = col.GetType();
					else
						p.SetValue(colStyle, typeof(DataGridViewColumn).GetProperty(p.Name).GetValue(col));
				style.ColumnsStyle.Add(colStyle);
			}

			style.RowHeadersWidth = this.RowHeadersWidth;
			style.AutoSizeColumnsMode = this.AutoSizeColumnsMode;
			if (this.SortedColumn != null)
			{
				style.SortColumnName = this.SortedColumn.Name;
				style.SortOrder = this.SortOrder == SortOrder.Ascending ? ListSortDirection.Ascending : ListSortDirection.Descending;
			}
			return style;
		}
		public void LoadStyle(GridStyle pStyle)
		{
			//按displayIndex排序,否则会因displayindex大于总列数而报错
			pStyle.ColumnsStyle.Sort((ColumnStyle x, ColumnStyle y) => { return x.DisplayIndex > y.DisplayIndex ? 1 : (x.DisplayIndex < y.DisplayIndex ? -1 : 0); });
			foreach (var style in pStyle.ColumnsStyle)
			{
				var col = this.Columns[style.Name];
				if (col == null || col.GetType() != style.ColumnType)
				{
					col = (DataGridViewColumn)Activator.CreateInstance(style.ColumnType);
					col.Name = style.Name;
					this.Columns.Add(col);
				}
				foreach (var p in typeof(ColumnStyle).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
					if (p.Name == "Format")
						col.DefaultCellStyle.Format = style.Format;
					else if (p.Name == "ColumnType")
						continue;
					//else if (p.Name == "DisplayIndex")
					//	continue;
					else if (p.Name != "Name")
						col.GetType().GetProperty(p.Name).SetValue(col, p.GetValue(style));

				//col.SortMode = style.Name == pStyle.SortColumnName ? DataGridViewColumnSortMode.Automatic : DataGridViewColumnSortMode.NotSortable;
			}
			this.RowHeadersWidth = pStyle.RowHeadersWidth;
			this.AutoSizeColumnsMode = pStyle.AutoSizeColumnsMode;
			if (!string.IsNullOrEmpty(pStyle.SortColumnName))
			{
				this.Columns[pStyle.SortColumnName].SortMode = DataGridViewColumnSortMode.Automatic;
				this.Sort(this.Columns[pStyle.SortColumnName], pStyle.SortOrder);
			}
		}

		public void ReLoadData(object[] pItems)
		{
			string sortCol = null;
			ListSortDirection dire = ListSortDirection.Ascending;
			//数据绑定
			if (this.SortedColumn != null) //不处理,则在resetbindings(false)时报错;若用(true)则排序消息
			{
				sortCol = this.SortedColumn.Name;
				dire = this.SortOrder == SortOrder.Ascending ? ListSortDirection.Ascending : ListSortDirection.Descending;
			}
			var bs = this.DataSource as BindingSource;
			if (bs == null) return;
			bs.RaiseListChangedEvents = false;
			bs.Clear();
			foreach (var o in pItems)
				bs.Add(o);
			bs.RaiseListChangedEvents = true;
			bs.ResetBindings(true);       //true时会导致原有的datagridview的columns重新创建!!!需要重新排序
			if (sortCol != null)
			{
				this.Columns[sortCol].SortMode = DataGridViewColumnSortMode.Automatic;
				this.Sort(this.Columns[sortCol], dire);
			}
		}
	}

	public class GridStyle
	{
		public List<ColumnStyle> ColumnsStyle { get; set; } = new List<ColumnStyle>();
		public int RowHeadersWidth { get; set; } = 15;
		public string SortColumnName { get; set; }
		public ListSortDirection SortOrder { get; set; }

		public DataGridViewAutoSizeColumnsMode AutoSizeColumnsMode { get; set; } = DataGridViewAutoSizeColumnsMode.None;
	}

	public class ColumnStyle
	{
		public string Name { get; set; } = string.Empty;
		public Type ColumnType { get; set; } = null;
		public DataGridViewAutoSizeColumnMode AutoSizeMode { get; set; } = DataGridViewAutoSizeColumnMode.NotSet;
		public string DataPropertyName { get; set; } = string.Empty;
		public int DisplayIndex { get; set; }
		public string HeaderText { get; set; } = string.Empty;
		public bool ReadOnly { get; set; } = false;
		public DataGridViewColumnSortMode SortMode { get; set; } = DataGridViewColumnSortMode.NotSortable;
		public Type ValueType { get; set; }
		public bool Visible { get; set; } = true;
		public int Width { get; set; } = 80;
		public string Format { get; set; } = string.Empty;

	}
}
