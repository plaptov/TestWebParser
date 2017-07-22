using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TestWebParser;

namespace TestConsoleApp
{
	class Program
	{
		static void Main(string[] args)
		{
			var docParser = new WebDocumentParser(new MockWebClient(), new MockHtmlParser());
			const int threadsCount = 50;
			int curThreadsCount = threadsCount;
			// Запускаем пучок потоков на скачивание и парсинг
			for (int i = 0; i < threadsCount; i++)
			{
				var url = i.ToString();
				new Thread(async () =>
				{
					Console.WriteLine(await docParser.DownloadAndParseDocument(url));
					// Считаем, сколько потоков осталось
					Interlocked.Decrement(ref curThreadsCount);
				}).Start();
			}
			// Ждём завершения всех потоков...
			while (curThreadsCount > 0)
			{
				Thread.Sleep(100);
			}
			Console.WriteLine("Finished");
			Console.ReadKey();
		}
	}
}
