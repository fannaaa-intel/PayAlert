Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Text
Imports PayAlert.Models

Namespace Services

    ''' <summary>One payment method's line in the Payment Summary report.</summary>
    Public NotInheritable Class MethodTotal
        Public Property Method As String = ""
        ''' <summary>Paid out on "I Owe" payments.</summary>
        Public Property Paid As Decimal
        ''' <summary>Received on "Owed to Me" payments.</summary>
        Public Property Collected As Decimal
        Public Property Records As Integer

        Public ReadOnly Property Total As Decimal
            Get
                Return Paid + Collected
            End Get
        End Property
    End Class

    ''' <summary>The Payment Summary report for a date range: totals per payment method, biggest first.</summary>
    Public NotInheritable Class MethodReport
        Public Property FromDate As Date
        Public Property ToDate As Date
        Public Property Methods As New List(Of MethodTotal)

        Public ReadOnly Property TotalPaid As Decimal
            Get
                Return Methods.Sum(Function(m) m.Paid)
            End Get
        End Property

        Public ReadOnly Property TotalCollected As Decimal
            Get
                Return Methods.Sum(Function(m) m.Collected)
            End Get
        End Property

        Public ReadOnly Property Total As Decimal
            Get
                Return TotalPaid + TotalCollected
            End Get
        End Property

        Public ReadOnly Property Records As Integer
            Get
                Return Methods.Sum(Function(m) m.Records)
            End Get
        End Property

        ''' <summary>A method's share of the total, 0 to 1.</summary>
        Public Function Share(item As MethodTotal) As Double
            Return If(Total = 0D, 0.0, CDbl(item.Total / Total))
        End Function
    End Class

    ''' <summary>
    ''' Builds the Payment Summary (totals by payment method) from recorded payments. Methods are grouped ignoring case and
    ''' extra spaces ("gcash" and "GCash" are one line); records without a method are listed as "Not set".
    ''' </summary>
    Public NotInheritable Class ReportService

        Public Const NotSet As String = "Not set"

        Private Sub New()
        End Sub

        Public Shared Function ByMethod(records As IEnumerable(Of PaymentTransaction), fromDate As Date, toDate As Date) As MethodReport
            Dim report As New MethodReport With {.FromDate = fromDate.Date, .ToDate = toDate.Date}
            Dim lines As New Dictionary(Of String, MethodTotal)(StringComparer.OrdinalIgnoreCase)
            For Each t In records
                If t.PaidOn < report.FromDate OrElse t.PaidOn > report.ToDate Then Continue For
                Dim name = CleanMethod(t.Method)
                Dim line As MethodTotal = Nothing
                If Not lines.TryGetValue(name, line) Then
                    line = New MethodTotal With {.Method = name}
                    lines.Add(name, line)
                End If
                If t.IsReceivable Then line.Collected += t.Amount Else line.Paid += t.Amount
                line.Records += 1
            Next
            ' Biggest first; "Not set" always last.
            report.Methods = lines.Values.
                OrderBy(Function(m) If(m.Method = NotSet, 1, 0)).
                ThenByDescending(Function(m) m.Total).
                ThenBy(Function(m) m.Method, StringComparer.CurrentCultureIgnoreCase).ToList()
            Return report
        End Function

        ''' <summary>
        ''' Trims and collapses spaces, and uses the standard spelling of a listed method ("gcash" becomes "GCash");
        ''' an empty method becomes "Not set".
        ''' </summary>
        Public Shared Function CleanMethod(method As String) As String
            Dim words = If(method, "").Split(New Char() {" "c}, StringSplitOptions.RemoveEmptyEntries)
            If words.Length = 0 Then Return NotSet
            Dim name = String.Join(" ", words)
            Dim known = PaymentTransaction.Methods.Concat({PaymentTransaction.MethodEWallet}).
                FirstOrDefault(Function(m) String.Equals(m, name, StringComparison.OrdinalIgnoreCase))
            Return If(known, name)
        End Function

        ''' <summary>Writes the report as a CSV file that opens in Excel (UTF-8 with BOM so ₱ shows correctly).</summary>
        Public Shared Sub WriteCsv(report As MethodReport, path As String)
            Dim money = Function(value As Decimal) value.ToString("0.00", CultureInfo.InvariantCulture)
            Dim csv As New StringBuilder()
            csv.AppendLine("PayAlert Payment Summary," & Quote(Format.ShortDate(report.FromDate) & " to " & Format.ShortDate(report.ToDate)))
            csv.AppendLine("Method,Paid (I Owe),Collected (Owed to Me),Total,Records,Share")
            For Each m In report.Methods
                csv.AppendLine(String.Join(",", Quote(m.Method), money(m.Paid), money(m.Collected), money(m.Total), m.Records.ToString(CultureInfo.InvariantCulture),
                                           (report.Share(m) * 100).ToString("0.0", CultureInfo.InvariantCulture) & "%"))
            Next
            csv.AppendLine(String.Join(",", "Total", money(report.TotalPaid), money(report.TotalCollected), money(report.Total),
                                       report.Records.ToString(CultureInfo.InvariantCulture), "100%"))
            File.WriteAllText(path, csv.ToString(), New UTF8Encoding(True))
        End Sub

        Private Shared Function Quote(value As String) As String
            Return """" & value.Replace("""", """""") & """"
        End Function

    End Class

End Namespace
