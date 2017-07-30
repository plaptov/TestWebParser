using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace TestWebParser
{
	/*
	Требуется реализовать обёртку над экземплярами IWebClient и IHtmlParser, которая:
	- Предоставляет единый асинхронный метод, позволяющий по ссылке скачать и распарсить IHtmlDocument.
	- Является полностью потокобезопасной.
	- Обеспечивает, что в каждый момент времени скачивается не более одной страницы и парсится не более одного документа.
	- При этом разрешает скачивание следующей страницы параллельно с парсингом предыдущего документа.
	*/

	/// <summary>
	/// Класс-обёртка, позволяющий потокобезопасно скачивать и парсить документы
	/// в один поток, параллеля эти процессы
	/// </summary>
	public class WebDocumentParser
	{
		/// <summary>
		/// Веб-клиент для скачивания HTML
		/// </summary>
		private IWebClient _webClient;
		/// <summary>
		/// Парсер HTML-документов
		/// </summary>
		private IHtmlParser _parser;
		/// <summary>
		/// Очередь на скачивание
		/// </summary>
		private ConcurrentQueue<DownloadStringRequest> _downloadQueue;
		/// <summary>
		/// Внутренний счётчик скачиваний в очереди
		/// </summary>
		private int _downloadCount;

		/// <summary>
		/// Создать экземпляр класса
		/// </summary>
		/// <param name="webClient">Реализация веб-клиента</param>
		/// <param name="parser">Реализация парсера документов</param>
		public WebDocumentParser(IWebClient webClient, IHtmlParser parser)
		{
			// Веб-клиент нужен обязательно
			_webClient = webClient ?? throw new ArgumentNullException(nameof(webClient));
			// Парсер нужен обязательно
			_parser = parser ?? throw new ArgumentNullException(nameof(parser));
			_downloadQueue = new ConcurrentQueue<DownloadStringRequest>();
		}

		/// <summary>
		/// Скачать и распарсить HTML-документ по URL
		/// </summary>
		/// <param name="url">URL страницы</param>
		/// <returns></returns>
		public Task<IHtmlDocument> DownloadAndParseDocument(string url)
		{
			// Создаём объект для завершения таска
			var tcs = new TaskCompletionSource<IHtmlDocument>();
			// Добавляем в очередь на скачивание
			_downloadQueue.Enqueue(new DownloadStringRequest(tcs, url));
			var cnt = Interlocked.Increment(ref _downloadCount);
			// Если добавили первый элемент в очередь, пускаем поток на обработку
			if (cnt == 1)
				ProcessDownloadQueue();
			// Возвращаем обещание
			return tcs.Task;
		}

		/// <summary>
		/// Метод обработки очереди на скачивание
		/// </summary>
		private async void ProcessDownloadQueue()
		{
			// Увеличиваем счётчик на фиктивную единицу, чтобы холостой цикл снаружи тоже считался как "занято"
			Interlocked.Increment(ref _downloadCount);
			string html = null;
			DownloadStringRequest curReq = null;
			DownloadStringRequest nextReq = null;
			while (true)
			{
				curReq = nextReq;
				if (curReq != null)
					html = await curReq.Task;
				// Проверяем, есть ли что-нибудь в очереди
				if (_downloadQueue.TryDequeue(out nextReq))
				{
					// Если есть, сразу запускаем таск на скачивание
					nextReq.Task = _webClient.GetStringAsync(nextReq.Url);
				}
				else if (curReq == null)
					break;
				// Скачиваем строку и добавляем в очередь на парсинг
				if (curReq != null)
				{
					curReq.Source.SetResult(_parser.Parse(html));
					// Уменьшаем счётчик в очереди на скачивание
					Interlocked.Decrement(ref _downloadCount);
				}
			}
			// Уменьшаем на фиктивную единицу, чтобы следующее добавление запустило новый поток
			Interlocked.Decrement(ref _downloadCount);
		}

		private class DownloadStringRequest
		{
			public DownloadStringRequest(TaskCompletionSource<IHtmlDocument> source, string url)
			{
				Source = source;
				Url = url;
			}

			public TaskCompletionSource<IHtmlDocument> Source { get; private set; }
			public string Url { get; private set; }

			public Task<string> Task { get; set; }
		}
	}
}
