using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace HaiFeng
{
	enum ProxyType
	{
		CTP,
		Tdx,
	}

	class FutureBroker
	{
		[DisplayName("接口类型")]
		public ProxyType Type { get; set; }

		[DisplayName("服务器")]
		public string Name { get; set; }
		[DisplayName("经纪商代码")]
		public string Broker { get; set; }
		[DisplayName("交易前置")]
		public string TradeAddr { get; set; }
		[DisplayName("行情前置")]
		public string QuoteAddr { get; set; }

		public override string ToString()
		{
			return Name;
		}
	}
}
