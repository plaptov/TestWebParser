﻿using System;
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
		/// Очередь на парсинг
		/// </summary>
		private ConcurrentQueue<DownloadStringResult> _parseQueue;
		/// <summary>
		/// Внутренний счётчик парсингов в очереди
		/// </summary>
		private int _parseCount;

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
			_parseQueue = new ConcurrentQueue<DownloadStringResult>();
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
				new Thread(ProcessDownloadQueue).Start();
			// Возвращаем обещание
			return tcs.Task;
		}

		/// <summary>
		/// Метод обработки очереди на скачивание
		/// </summary>
		private async void ProcessDownloadQueue()
		{
			// Чтоб не создавать новые потоки на каждую незначительную задержку,
			// допускаем некоторое ожидание вхолостую, тут - секунда
			const int maxTryCount = 100;
			const int waitPeriod = 10;
			int tryCount = 0;
			// Увеличиваем счётчик на фиктивную единицу, чтобы холостой цикл снаружи тоже считался как "занято"
			Interlocked.Increment(ref _downloadCount);
			while (tryCount < maxTryCount)
			{
				DownloadStringRequest req;
				// Проверяем, есть ли что-нибудь в очереди
				if (!_downloadQueue.TryDequeue(out req))
				{
					// Если пусто, подождём немножко
					tryCount++;
					// Может, SpinWait подошёл бы лучше, чтобы не переключать контекст, но я с ним не работал
					Thread.Sleep(waitPeriod);
					continue;
				}
				// В следующий раз ждать начнём сначала
				tryCount = 0;
				// Скачиваем строку и добавляем в очередь на парсинг
				_parseQueue.Enqueue(new DownloadStringResult(req.Source, await _webClient.GetStringAsync(req.Url)));
				// Уменьшаем счётчик в очереди на скачивание
				Interlocked.Decrement(ref _downloadCount);
				var cnt = Interlocked.Increment(ref _parseCount);
				// Если добавили первый элемент в очередь, запускаем поток на обработку
				if (cnt == 1)
					new Thread(ProcessParseQueue).Start();
			}
			// Уменьшаем на фиктивную единицу, чтобы следующее добавление запустило новый поток
			Interlocked.Decrement(ref _downloadCount);
		}

		/// <summary>
		/// Метод обработки очереди на парсинг.
		/// Почти идентичен предыдущему
		/// </summary>
		private void ProcessParseQueue()
		{
			const int maxTryCount = 100;
			const int waitPeriod = 10;
			int tryCount = 0;
			Interlocked.Increment(ref _parseCount);
			while (tryCount < maxTryCount)
			{
				DownloadStringResult req;
				if (!_parseQueue.TryDequeue(out req))
				{
					tryCount++;
					Thread.Sleep(waitPeriod);
					continue;
				}
				tryCount = 0;
				// Парсим документ и подтверждаем таск
				req.Source.SetResult(_parser.Parse(req.Text));
				Interlocked.Decrement(ref _parseCount);
			}
			Interlocked.Decrement(ref _parseCount);
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
		}

		private class DownloadStringResult
		{
			public DownloadStringResult(TaskCompletionSource<IHtmlDocument> source, string text)
			{
				Source = source;
				Text = text;
			}

			public TaskCompletionSource<IHtmlDocument> Source { get; private set; }
			public string Text { get; private set; }
		}
	}
}
