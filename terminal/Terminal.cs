using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaiFeng
{
	partial class HFForm
	{

		void LogFinish()
		{
			if (File.Exists("沪深A股.txt"))
			{
				foreach (var line in (File.ReadAllLines("沪深A股.txt")))
				{
					if (char.IsDigit(line[0]))
					//this.comboBoxInstrument.Items.Add(line.Split('\t')[0]);
					{
						_t.DicInstrumentField[line.Split('\t')[0]] = new InstrumentField
						{
							InstrumentID = line.Split('\t')[0],
							//ExchangeID = "sh",							
							PriceTick = 0.01,
							ProductClass = ProductClassType.SpotOption,
							VolumeMultiple = 1,
							MaxOrderVolume = 1000000000,
							//ProductID
						};
					}
				}
				this.comboBoxInstrument.Items.AddRange(_t.DicInstrumentField.Keys.ToArray());
			}
		}
	}
}
