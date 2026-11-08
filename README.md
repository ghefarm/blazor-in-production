# Blazor in Production: the example app

The code of Foresight, the example app of the book *Blazor in Production*
by Ghefar Mansour.

The book is enough on its own: every chapter tells you which files to
create and change, and prints them. This repository is there to check your
work. Each folder holds the complete app as it stands at the end of one
chapter:

| Folder | Chapter |
|---|---|
| `ch01` | 1 The App We Are Going to Break |
| `ch02` | 2 The Page That Forgot Where You Came From |
| `ch03` | 3 One Database, and How It Grows |
| `ch04` | 4 A Login You Do Not Have to Invent |
| `ch05` | 5 The Handler That Never Saw the Token |
| `ch06` | 6 A Mail That Knows When to Stop |
| `ch07` | 7 A Token Is Not an Account |
| `ch08` | 8 A Subscription Is a State Machine |
| `ch09` | 9 Anyone Can POST to Your Webhook |
| `ch10` | 10 Who Owns This Subscription? |
| `ch11` | 11 Cancel First, Then Forget |
| `ch12` | 12 Breaking Payments on Purpose |
| `ch13` | 13 One Forecast, Two Channels |
| `ch14` | 14 The Button Is Not the Gate |
| `ch15` | 15 Posting Without a Queue |
| `ch16` | 16 When Nobody Is Watching |
| `ch17` | 17 A Link of Their Own |
| `ch18` | 18 One Account, One Vote |
| `ch19` | 19 A Track Record That Shows Its Misses |
| `ch20` | 20 Drawing the Enemy |
| `ch21` | 21 The Key in the Repository |
| `ch22` | 22 A Limit for Every Visitor |
| `ch23` | 23 Only Through the Front Door |
| `ch24` | 24 Yes or No, and Nothing More |
| `ch25` | 25 Shipping to a Windows Host |
| `ch26` | 26 Tests That Talk HTTP |
| `ch27` | 27 What It Costs to Keep Running |

Every folder is generated from the text of the book: a script does exactly
what the chapters print, starting from an empty folder, and runs the
chapter's Checkpoint. If a folder and the book disagree, please open an
issue.

## Running a chapter

You need the .NET 10 SDK, and from chapter 3 on the EF Core tool:

```
dotnet tool install --global dotnet-ef --version 10.0.14
```

Then, in the folder of a chapter, for example `ch03`:

```
dotnet dev-certs https --trust
dotnet test --filter Chapter=3
dotnet run --project src/Foresight.Api
dotnet run --project src/Foresight.Web
```

Start the API and the website in two terminals. The website runs at
`https://localhost:7071` and calls the API at `https://localhost:7030`.
The tests need nothing more; they bring their own settings.

From chapter 4 on, the API also needs the key that signs its login
tokens. It is a secret, so it is not in this repository. Set it once on
your machine; every chapter folder uses the same user secrets:

```
dotnet user-secrets set "Jwt:Key" "<64 random characters>" --id foresight-api
```

On Windows, PowerShell makes such a value with
`[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))`;
on Linux and macOS, `openssl rand -hex 32` does the same. The PayPal
sandbox (Part III) and the Telegram bot (Part IV) are your own as well;
Appendix A of the book shows how to set them up.

## License

MIT, see [LICENSE](LICENSE).
