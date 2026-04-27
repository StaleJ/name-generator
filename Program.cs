using Bogus;
using Bogus.DataSets;
using System.Text.Json;

int age;
while (true)
{
    Console.Write("Alder (heltall): ");
    if (int.TryParse(Console.ReadLine(), out age) && age is >= 0 and <= 120)
        break;
    Console.WriteLine("Ugyldig alder. Skriv inn et heltall mellom 0 og 120.");
}

bool isMale;
while (true)
{
    Console.Write("Kjønn (han/hun): ");
    var raw = Console.ReadLine()?.Trim().ToLowerInvariant();
    if (raw is "han") { isMale = true; break; }
    if (raw is "hun") { isMale = false; break; }
    Console.WriteLine("Ugyldig valg. Skriv 'han' eller 'hun'.");
}

var today = DateOnly.FromDateTime(DateTime.Today);
int birthYear = today.Year - age;
var rng = new Random();
int month = rng.Next(1, 13);
int day   = rng.Next(1, DateTime.DaysInMonth(birthYear, month) + 1);

int iMin, iMax;
if      (birthYear is >= 1900 and <= 1999) { iMin = 0;   iMax = 499; }
else if (birthYear is >= 2000 and <= 2039) { iMin = 500; iMax = 999; }
else throw new InvalidOperationException($"Fødselsår {birthYear} støttes ikke.");

int[] d = new int[9];
d[0] = day / 10;    d[1] = day % 10;
d[2] = month / 10;  d[3] = month % 10;
int yy = birthYear % 100;
d[4] = yy / 10;     d[5] = yy % 10;

int start = rng.Next(iMin, iMax + 1);
string personnummer = "";

for (int offset = 0; offset <= (iMax - iMin); offset++)
{
    int cand = iMin + ((start - iMin + offset) % (iMax - iMin + 1));
    if ((cand % 2 == 1) != isMale) continue;

    d[6] = cand / 100;
    d[7] = (cand / 10) % 10;
    d[8] = cand % 10;

    int s1 = 3*d[0] + 7*d[1] + 6*d[2] + 1*d[3] + 8*d[4] + 9*d[5] + 4*d[6] + 5*d[7] + 2*d[8];
    int r1 = s1 % 11;
    int k1 = r1 == 0 ? 0 : 11 - r1;
    if (k1 == 10) continue;

    int s2 = 5*d[0] + 4*d[1] + 3*d[2] + 2*d[3] + 7*d[4] + 6*d[5] + 5*d[6] + 4*d[7] + 3*d[8] + 2*k1;
    int r2 = s2 % 11;
    int k2 = r2 == 0 ? 0 : 11 - r2;
    if (k2 == 10) continue;

    personnummer = $"{day:D2}{month:D2}{yy:D2}{cand:D3}{k1}{k2}";
    break;
}

if (personnummer.Length == 0)
    throw new InvalidOperationException("Fant ikke gyldig personnummer – dette skal ikke skje.");

var gender    = isMale ? Name.Gender.Male : Name.Gender.Female;
var faker     = new Faker("nb_NO");
var firstName = faker.Name.FirstName(gender);
var lastName  = faker.Name.LastName(gender);
var phone     = faker.Phone.PhoneNumber();

Console.WriteLine();
Console.WriteLine($"Fornavn:       {firstName}");
Console.WriteLine($"Etternavn:     {lastName}");
Console.WriteLine($"Personnummer:  {personnummer}");
Console.WriteLine($"Telefon:       {phone}");

Console.Write("\nBankID preprod aktivering? (j/n): ");
if (Console.ReadLine()?.Trim().ToLowerInvariant() == "j")
{
    using var http = new HttpClient();
    http.DefaultRequestHeaders.Add("accept", "application/json");
    http.DefaultRequestHeaders.Add("origin", "https://ra-preprod.bankidnorge.no");
    http.DefaultRequestHeaders.Add("referer", "https://ra-preprod.bankidnorge.no/");

    var body = JsonSerializer.Serialize(new
    {
        commonName = $"{lastName}, {firstName}",
        bankIDFriendlyName = $"{firstName} {lastName}",
        otpServiceNameIndexes = new[] { 1 }
    });

    var postUrl = $"https://ra-preprod.bankidnorge.no/api/enduser/{personnummer}/bankid/netcentric";
    var postContent = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
    var postResp = await http.PostAsync(postUrl, postContent);

    if (!postResp.IsSuccessStatusCode)
    {
        Console.WriteLine($"✗ Registrering feilet: {(int)postResp.StatusCode}");
    }
    else
    {
        var pollUrl = $"https://ra-preprod.bankidnorge.no/api/enduser/{personnummer}/bankid/netcentric/0";
        http.DefaultRequestHeaders.Add("cache-control", "no-cache");

        bool activated = false;
        for (int attempt = 1; attempt <= 5; attempt++)
        {
            Console.Write($"Sjekker status ({attempt}/5)...");
            await Task.Delay(2000);
            var pollResp = await http.GetAsync(pollUrl);
            var json = await pollResp.Content.ReadAsStringAsync();
            if (json.Contains("ACTIVATED", StringComparison.OrdinalIgnoreCase))
            {
                activated = true;
                break;
            }
            Console.WriteLine(" venter...");
        }

        Console.WriteLine(activated
            ? $"\n✓ BankID aktivert for {personnummer}"
            : "\n✗ Aktivering ikke fullført etter 5 forsøk.");
    }
}
