using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Infrastructure;

/// <summary>
/// Jedyny punkt dostępu do bazy. Celowo bez repozytoriów per encja — dostęp do danych
/// żyje w handlerach feature'ów (patrz CLAUDE.md §4), żeby przyszły multi-user dało się
/// dołożyć jednym global query filter zamiast przepisywania warstwy dostępu.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<BudgetItem> BudgetItems => Set<BudgetItem>();
    public DbSet<CategoryRule> CategoryRules => Set<CategoryRule>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<SavingsGoal> SavingsGoals => Set<SavingsGoal>();
    public DbSet<SavingsReservation> SavingsReservations => Set<SavingsReservation>();
    public DbSet<StandingOrder> StandingOrders => Set<StandingOrder>();
    public DbSet<EpisodicOrder> EpisodicOrders => Set<EpisodicOrder>();

    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<TransactionStatusDictionary> TransactionStatuses => Set<TransactionStatusDictionary>();
    public DbSet<RuleDirectionDictionary> RuleDirections => Set<RuleDirectionDictionary>();
    public DbSet<AccountTypeDictionary> AccountTypes => Set<AccountTypeDictionary>();
    public DbSet<StandingOrderRhythmDictionary> StandingOrderRhythms => Set<StandingOrderRhythmDictionary>();

    /// <summary>
    /// Kwoty pieniężne zawsze <c>numeric(18,2)</c> — nigdy float/double. Konwencja globalna,
    /// żeby nie dało się o tym zapomnieć przy nowej encji.
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.Properties<decimal>().HavePrecision(18, 2);
    }

    /// <remarks>
    /// <list type="bullet">
    /// <item>Indeksy unikalne na danych z soft delete są CZĘŚCIOWE (<see cref="AliveOnly"/>) — bez filtra
    /// skasowany wiersz dalej zajmowałby swoją wartość i blokowałby utworzenie nowego.</item>
    /// <item><c>Transaction.Confidence</c> ma jawne <c>numeric(4,3)</c>: 18,2 z konwencji zaokrągliłoby
    /// 0.734 do 0.73 i zniszczyło porównanie z progiem pewności.</item>
    /// <item>Kolumny <c>BudgetBusinessId</c> (transakcje, limity, cele, rezerwacje) to ŚWIADOMIE zwykłe
    /// kolumny bez relacji EF — handlery filtrują po nich indeksem, nie joinem po kluczu obcym.</item>
    /// <item>Kolumny słownikowe (<c>Status</c>, <c>Direction</c>, <c>Type</c>, <c>Currency</c>) trzymają kod,
    /// nie liczbę, i mają klucz obcy do słownika (CLAUDE.md §5).</item>
    /// <item>Klucze obce mają <c>DeleteBehavior.Restrict</c>: nic nie jest kasowane fizycznie poza
    /// sprzątaniem po oknie retencji, więc kaskada nie ma czego robić (CLAUDE.md §4).</item>
    /// </list>
    /// </remarks>
    protected override void OnModelCreating(ModelBuilder b)
    {
        ConfigureIdentityAndSoftDelete(b);
        ConfigureDictionaries(b);

        b.Entity<Category>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Name).IsUnique().HasFilter(AliveOnly);
        });

        b.Entity<Account>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(DictionaryCodeLength);
            e.HasOne<AccountTypeDictionary>().WithMany()
                .HasForeignKey(x => x.Type).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Transaction>(e =>
        {
            e.Property(x => x.Description).HasMaxLength(500).IsRequired();
            e.Property(x => x.Confidence).HasPrecision(4, 3);
            e.Property(x => x.CreatedAt).HasColumnType("timestamptz");

            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(DictionaryCodeLength);
            e.HasOne<TransactionStatusDictionary>().WithMany()
                .HasForeignKey(x => x.Status).OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => new { x.Date, x.CategoryId });
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.StandingOrderBusinessId);
            e.HasIndex(x => x.SavingsTransferBudgetBusinessId);

            e.HasOne<Category>().WithMany()
                .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);

            e.HasOne<Account>().WithMany()
                .HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => x.BudgetBusinessId);

            e.Property(x => x.ExternalReference).HasMaxLength(100);
            e.Property(x => x.TransactionType).HasMaxLength(80);

            e.HasIndex(x => new { x.Date, x.Amount, x.ExternalReference });

            e.HasOne<ImportBatch>().WithMany()
                .HasForeignKey(x => x.ImportBatchId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<CategoryRule>(e =>
        {
            e.Property(x => x.Pattern).HasMaxLength(200);
            e.Property(x => x.TransactionTypePattern).HasMaxLength(200);
            e.Property(x => x.Note).HasMaxLength(300);
            e.Property(x => x.Direction).HasConversion<string>().HasMaxLength(DictionaryCodeLength);
            e.HasOne<RuleDirectionDictionary>().WithMany()
                .HasForeignKey(x => x.Direction).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.Priority);
            e.HasOne<Category>().WithMany()
                .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ImportBatch>(e =>
        {
            e.Property(x => x.FileName).HasMaxLength(260).IsRequired();
            e.Property(x => x.Bank).HasMaxLength(40);
            e.Property(x => x.ImportedAt).HasColumnType("timestamptz");
            e.HasOne<Budget>().WithMany()
                .HasForeignKey(x => x.BudgetId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Currency>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(3);
            e.HasData(new Currency("PLN"));
        });

        b.Entity<TransactionStatusDictionary>(e =>
        {
            e.ToTable("TransactionStatuses");
            e.HasData(Enum.GetValues<TransactionStatus>().Select(code => new TransactionStatusDictionary(code)));
        });

        b.Entity<RuleDirectionDictionary>(e =>
        {
            e.ToTable("RuleDirections");
            e.HasData(Enum.GetValues<RuleDirection>().Select(code => new RuleDirectionDictionary(code)));
        });

        b.Entity<AccountTypeDictionary>(e =>
        {
            e.ToTable("AccountTypes");
            e.HasData(Enum.GetValues<AccountType>().Select(code => new AccountTypeDictionary(code)));
        });

        b.Entity<StandingOrderRhythmDictionary>(e =>
        {
            e.ToTable("StandingOrderRhythms");
            e.HasData(Enum.GetValues<StandingOrderRhythm>().Select(code => new StandingOrderRhythmDictionary(code)));
        });

        b.Entity<StandingOrder>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnType("timestamptz");
            // Reguły jako jsonb: wartość zlecenia bez własnego cyklu życia — kasują się, przywracają i purge'ują
            // razem z nim, bez osobnej tabeli do pilnowania w BudgetChildren/BudgetPurger.
            e.ComplexCollection(x => x.Rules, r => r.ToJson());
            e.Property(x => x.Rhythm).HasConversion<string>().HasMaxLength(DictionaryCodeLength);
            e.HasOne<StandingOrderRhythmDictionary>().WithMany()
                .HasForeignKey(x => x.Rhythm).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.BudgetBusinessId);
        });

        b.Entity<EpisodicOrder>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.CreatedAt).HasColumnType("timestamptz");
            e.HasOne<Category>().WithMany()
                .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.BudgetBusinessId);
            // Jedna transakcja = jedno zlecenie epizodyczne; skasowane zlecenie nie blokuje oznaczenia jej od nowa.
            e.HasIndex(x => x.TransactionBusinessId)
                .IsUnique()
                .HasFilter($"\"TransactionBusinessId\" IS NOT NULL AND {AliveOnly}");
        });

        b.Entity<Budget>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnType("timestamptz");
            e.Property(x => x.DisabledAt).HasColumnType("timestamptz");
            e.Property(x => x.InitialBalance).HasColumnType("numeric(18,2)");
            e.HasOne<Currency>().WithMany()
                .HasForeignKey(x => x.Currency).OnDelete(DeleteBehavior.Restrict);
            // Reguły transferu jako jsonb: część zasad TEGO budżetu, bez własnego cyklu życia —
            // kasują się, przywracają i purge'ują razem z nim, bez osobnej tabeli.
            e.ComplexCollection(x => x.SavingsTransferRules, r => r.ToJson());
        });

        b.Entity<BudgetItem>(e =>
        {
            // Unikalność PER MIESIĄC STARTU, nie per kategoria: z historią limitów ta sama kategoria
            // ma w budżecie wiele wierszy — po jednym na każdą zmianę kwoty.
            e.HasIndex(x => new { x.BudgetBusinessId, x.CategoryId, x.ValidFrom }).IsUnique().HasFilter(AliveOnly);
            e.HasOne<Category>().WithMany()
                .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<SavingsGoal>(e =>
        {
            e.Property(x => x.Amount).HasColumnType("numeric(18,2)");
            e.Property(x => x.CreatedAt).HasColumnType("timestamptz");
            e.HasIndex(x => new { x.BudgetBusinessId, x.StartedOn });
        });

        b.Entity<SavingsReservation>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Amount).HasColumnType("numeric(18,2)");
            e.Property(x => x.CreatedAt).HasColumnType("timestamptz");
            e.Property(x => x.SettledAt).HasColumnType("timestamptz");
            // Wpłaty jako jsonb: wartość rezerwacji bez własnego cyklu życia — kasują się i przywracają razem z nią.
            e.ComplexCollection(x => x.Contributions, c => c.ToJson());
            e.HasIndex(x => new { x.BudgetBusinessId, x.DueMonth, x.Priority });
            e.HasIndex(x => x.SettledTransactionBusinessId)
                .IsUnique()
                .HasFilter($"\"SettledTransactionBusinessId\" IS NOT NULL AND {AliveOnly}");
        });
    }

    /// <summary>Warunek indeksu częściowego — tylko żywe wiersze zajmują miejsce w indeksie.</summary>
    private const string AliveOnly = "\"DeletedAt\" IS NULL";

    /// <summary>Długość kolumny z kodem słownika — wspólna dla klucza słownika i kolumn, które się do niego odwołują.</summary>
    private const int DictionaryCodeLength = 50;

    /// <summary>
    /// Unikalny indeks na kluczu publicznym i filtr soft delete zakładane PĘTLĄ — nowa encja
    /// dziedzicząca po <see cref="Entity"/> dostaje jedno i drugie bez dopisywania czegokolwiek tutaj.
    /// </summary>
    /// <remarks>
    /// ⚠️ Filtr globalny działa w obie strony: zapytanie, które ŚWIADOMIE chce zobaczyć skasowane,
    /// musi poprosić przez <c>IgnoreQueryFilters()</c>, inaczej cicho zwróci pustkę.
    /// </remarks>
    private static void ConfigureIdentityAndSoftDelete(ModelBuilder b)
    {
        foreach (var entity in b.Model.GetEntityTypes())
        {
            var type = entity.ClrType;
            if (!typeof(Entity).IsAssignableFrom(type)) continue;

            b.Entity(type).HasIndex(nameof(Entity.BusinessId)).IsUnique();
            b.Entity(type).Property(nameof(Entity.DeletedAt)).HasColumnType("timestamptz");
            b.Entity(type).HasQueryFilter(SoftDeleteFilter(type));
        }
    }

    /// <summary>
    /// Słowniki (<see cref="DictionaryEntity"/>) mają kod jako klucz główny, a kod typu enum trafia do bazy jako nazwa.
    /// Konwencja EF nie rozpozna <c>Code</c> jako klucza sama, więc zakłada go pętla — konkretny słownik może potem
    /// zawęzić długość.
    /// </summary>
    private static void ConfigureDictionaries(ModelBuilder b)
    {
        foreach (var entity in b.Model.GetEntityTypes())
        {
            var type = entity.ClrType;
            if (!typeof(DictionaryEntity).IsAssignableFrom(type)) continue;

            var code = b.Entity(type).Property("Code");
            b.Entity(type).HasKey("Code");
            code.HasMaxLength(DictionaryCodeLength);

            if (code.Metadata.ClrType.IsEnum)
            {
                code.HasConversion(typeof(string));
            }
        }
    }

    /// <summary>
    /// <c>e =&gt; e.DeletedAt == null</c> zbudowane dla konkretnego typu — <c>HasQueryFilter</c>
    /// w wariancie nietypowanym wymaga wyrażenia, a nie lambdy generycznej.
    /// </summary>
    private static LambdaExpression SoftDeleteFilter(Type entityType)
    {
        var parameter = Expression.Parameter(entityType, "e");
        var deletedAt = Expression.Property(parameter, nameof(Entity.DeletedAt));
        var isNull = Expression.Equal(deletedAt, Expression.Constant(null, typeof(DateTimeOffset?)));

        return Expression.Lambda(isNull, parameter);
    }
}
