using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace HaiFeng
{
	class Account
	{
		[DisplayName("服务器")]
		public string ServerName { get; set; } = string.Empty;
		[DisplayName("帐号")]
		public string Investor { get; set; } = string.Empty;

		public override string ToString()
		{
			return $"{Investor}@{ServerName}";
		}
	}
}
