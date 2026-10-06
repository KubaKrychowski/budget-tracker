## Transakcje i kategorie

Tu zobaczysz **wszystkie swoje transakcje w jednym miejscu** — przeszukasz je, przefiltrujesz, poprawisz im kategorie i zobaczysz, które wymagają Twojej uwagi.

![Lista transakcji z kategoriami, statusem, filtrami i akcjami dla zaznaczonych wierszy](handbook/images/transactions-list.png)

> 🔒 **Historia jest zawsze 1:1 z bankiem.** Import nigdy nie zmienia opisu ani kwoty transakcji. Jedyne, co aplikacja dopisuje, to **kategoria** (oraz znaczniki: zlecenie stałe, zlecenie epizodyczne, transfer).

### Jak znaleźć transakcję

Nad tabelą masz trzy narzędzia:

- **Szukaj po opisie** — wpisz fragment nazwy (np. „market”) i kliknij lupę,
- **Data od – do** — zawęź do wybranego okresu,
- **Budżet** — wybierz jeden lub kilka budżetów naraz.

Nagłówki kolumn można **sortować** (strzałki) i **filtrować** (lejek) — np. po kategorii albo statusie. Kafle **Podsumowanie** nad tabelą liczą się dla tego, co aktualnie widzisz.

### Statusy transakcji

| Status | Co znaczy |
|---|---|
| 🟠 **Skategoryzowana automatycznie** | Aplikacja była pewna i sama przypisała kategorię. |
| 🔵 **Do przeglądu** | Aplikacja nie była pewna — czeka na Twoją decyzję. |
| 🟢 **Zatwierdzona** | Kategoria jest potwierdzona — przez Ciebie albo przez regułę. |

Szybko znajdziesz transakcje do przeglądu, filtrując kolumnę **Status**.

### Jak zmienić kategorię

**Jedna transakcja:** w kolumnie **Akcje** (⋮) wybierz **Edytuj** i wskaż właściwą kategorię.

**Wiele naraz:** zaznacz transakcje checkboxami po lewej, a następnie użyj przycisków nad tabelą:

- **Masowe ustawienie kategorii** — jedna kategoria dla wszystkich zaznaczonych,
- **Edytuj zaznaczone**,
- **Usuń zaznaczone**.

Przyciski są wyszarzone, dopóki niczego nie zaznaczysz.

![Menu akcji przy transakcji: Edytuj, Oznacz jako zlecenie epizodyczne, Usuń](handbook/images/transactions-menu.png)

W menu **⋮** znajdziesz też **Oznacz jako zlecenie epizodyczne…** — gdy transakcja to większy, jednorazowy zakup (więcej w temacie **Zlecenia epizodyczne**).

### Jak aplikacja wybiera kategorię

Działa dwutorowo:

1. **Reguły** łapią oczywiste przypadki (np. konkretny sprzedawca zawsze trafia do tej samej kategorii).
2. **Model uczenia maszynowego** zajmuje się resztą — patrzy na opis, kwotę i to, czy to wydatek, czy przychód.

Co z tego wynika dla Ciebie:

- Pewność powyżej progu → kategoria przypisuje się **sama**.
- Pewność poniżej progu → transakcja ląduje w „Do przeglądu”.
- **Każda Twoja poprawka (także zatwierdzenie podpowiedzi) uczy model.** Im więcej poprawisz, tym rzadziej będziesz musiał to robić.
- **Przychody** dostają kategorię wyłącznie z reguł — model uczy się tylko na wydatkach. Jeśli wpływ nie ma kategorii, dodaj regułę (**Ustawienia → Reguły predykatu**).

### Transfer na budżet oszczędnościowy

Jeśli budżet ma powiązany budżet oszczędnościowy, przelewy między nimi są oznaczone jako **„Transfer”** i nie liczą się do wydatków ani przychodów — ale kategoria i opis zostają nietknięte. Jeśli reguła dopasowała się niesłusznie, zdejmiesz znacznik w menu transakcji: **Odepnij transfer**.

### Wejście z innych ekranów

Gdy przejdziesz tu ze zlecenia stałego lub epizodycznego („Przejdź do powiązanych”), lista pokaże tylko transakcje tego zlecenia. Z powrotem wrócisz okruszkiem nawigacji u góry.
