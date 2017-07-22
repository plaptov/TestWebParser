using System;
using System.Threading.Tasks;

namespace TestWebParser
{
	public interface IWebClient
	{
		Task<string> GetStringAsync(string urlText);
	}

	public interface IHtmlParser
	{
		IHtmlDocument Parse(string htmlText);
	}

	public interface IHtmlDocument
	{
		// Много интересных свойств, не критичных в рамках данной задачи
	}
}
