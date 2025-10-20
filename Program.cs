// See https://aka.ms/new-console-template for more information

using Bogus;

var faker = new Faker("nb_NO");
var output = $"""
              Fornavn: {faker.Person.FirstName}
              Etternavn: {faker.Person.LastName}
              Telefon: {faker.Person.Phone}
              """;
Console.WriteLine(output);