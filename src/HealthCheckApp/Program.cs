using var http = new HttpClient();

var url = args.FirstOrDefault();

if (string.IsNullOrEmpty(url))
{
    throw new Exception("Missing URL");
}

var response = await http.GetAsync(url);
response.EnsureSuccessStatusCode();

if (response.Content.Headers.ContentType?.MediaType != "application/json" || response.Content.Headers.ContentLength == 0)
{
    throw new Exception("Invalid ContentType or Length");
}

Console.WriteLine("OK");
return 0;
