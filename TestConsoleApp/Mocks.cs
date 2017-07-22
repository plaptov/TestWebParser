using System;
using System.Threading;
using System.Threading.Tasks;
using TestWebParser;

namespace TestConsoleApp
{
	public class MockWebClient : IWebClient
	{
		private static Random rnd = new Random();
		int callCount;

		public async Task<string> GetStringAsync(string urlText)
		{
			if (Interlocked.Increment(ref callCount) != 1)
				throw new Exception("Parallel downloading");
			await Task.Delay(rnd.Next(1000));
			if (Interlocked.Decrement(ref callCount) != 0)
				throw new Exception("Parallel downloading");
			return urlText;
		}
	}

	public class MockHtmlParser : IHtmlParser
	{
		private static Random rnd = new Random();
		int callCount;

		public IHtmlDocument Parse(string htmlText)
		{
			if (Interlocked.Increment(ref callCount) != 1)
				throw new Exception("Parallel parsing");
			Thread.Sleep(rnd.Next(1000));
			if (Interlocked.Decrement(ref callCount) != 0)
				throw new Exception("Parallel downloading");
			return new MockHtmDocument(htmlText);
		}
	}

	public class MockHtmDocument : IHtmlDocument
	{
		public MockHtmDocument(string text)
		{
			Text = text;
		}
		public string Text { get; set; }

		public override string ToString()
		{
			return Text;
		}
	}
}
