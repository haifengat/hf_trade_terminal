using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace HaiFeng
{
	static class Program
	{
		private static string _errLog = string.Empty;
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main()
		{
			_errLog = "err_" + Application.ProductName + ".log";
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);

			//你在主线程捕获全部异常就行，如下代码： 
			//WINFORM未处理异常之捕获 
			//处理未捕获的异常 
			Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
			//处理UI线程异常 
			Application.ThreadException += Application_ThreadException;
			//处理非UI线程异常 
			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

			Application.Run(new HFForm());
		}
		#region 处理未捕获异常的挂钩函数

		private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
		{
			Exception error = e.Exception;
			if (error != null)
			{
				using (StreamWriter sw = new StreamWriter(_errLog, true))
				{
					sw.WriteLine(DateTime.Now.ToString("yyyyMMdd HH:mm:ss") + "\t" + string.Format("出现应用程序未处理的异常 异常类型：{0} 异常消息：{1} 异常位置：{2} ", error.GetType().Name, error.Message, error.StackTrace));
				}
			}
			else
			{
				using (StreamWriter sw = new StreamWriter(_errLog, true))
				{
					sw.WriteLine(DateTime.Now.ToString("yyyyMMdd HH:mm:ss") + "\t" + string.Format("Application ThreadError:{0}", e));
				}
			}
		}

		private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			Exception error = e.ExceptionObject as Exception;
			if (error != null)
			{
				using (StreamWriter sw = new StreamWriter(_errLog, true))
				{
					sw.WriteLine(DateTime.Now.ToString("yyyyMMdd HH:mm:ss") + "\t" + string.Format("Application UnhandledException:{0}; 堆栈信息:{1}", error.Message, error.StackTrace));
				}
			}
			else
			{
				using (StreamWriter sw = new StreamWriter(_errLog, true))
				{
					sw.WriteLine(DateTime.Now.ToString("yyyyMMdd HH:mm:ss") + "\t" + string.Format("Application UnhandledError:{0}", e));
				}
			}
		}
		#endregion
	}
}
