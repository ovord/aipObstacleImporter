﻿Imports System.Text.RegularExpressions
Imports iTextSharp.text.pdf
Imports iTextSharp.text.pdf.parser
Imports Microsoft.SmallBasic.Library

Imports DotSpatial.Data
Imports DotSpatial.Topology
Imports System.IO

Module Module1

    Dim Version As String = "1.0"


    Dim parseResult As New List(Of obstacleStruct)
    Structure obstacleStruct
        Dim name As String
        Dim type As String
        Dim elevation As Short
        Dim lat As Double
        Dim lon As Double
        Dim marked As Boolean
        Dim lighted As Boolean
        Dim height As String
    End Structure

    ' header
    Sub writeHeader()
        Console.BackgroundColor = ConsoleColor.Gray
        Console.ForegroundColor = ConsoleColor.Black

        Console.WriteLine("(c) neway data AG - Landstraße 105 - 9490 Vaduz - FL-0002.103.140-4")
        Console.WriteLine("AIP OBSTACLE IMPORTER" & "    VERSION: " & Version & " ")
        Console.ResetColor()
    End Sub

    Public Function ParsePdfText(ByVal sourcePDF As String,
                                  Optional ByVal fromPageNum As Integer = 0,
                                  Optional ByVal toPageNum As Integer = 0) As String

        Dim sb As New System.Text.StringBuilder()
        Try
            Dim reader As New PdfReader(sourcePDF)
            If fromPageNum = 0 Then
                fromPageNum = 1
            End If
            If toPageNum = 0 Then
                toPageNum = reader.NumberOfPages
            End If

            If fromPageNum > toPageNum Then
                Throw New ApplicationException("Parameter error: The value of fromPageNum can " &
                                           "not be larger than the value of toPageNum")
            End If

            Dim text As String = ""
            For i As Integer = fromPageNum To toPageNum Step 1
                text &= PdfTextExtractor.GetTextFromPage(reader, i)
            Next i

            processText(text)

        Catch ex As Exception

            Return String.Empty
        End Try
        Return sb.ToString()
    End Function

    Sub processText(rawtext As String)
        Dim pattern As String = "[NS] [0-8][0-9] [0-5][0-9] [0-9][0-9]"

        ' Instantiate the regular expression object.
        Dim r As Regex = New Regex(pattern, RegexOptions.IgnoreCase)

        Dim m As Match = r.Match(rawtext)
        Dim matchcount As Integer = 0
        Do While m.Success
            Dim targetAddress = m.Index
            Dim PreBufferLength As Short = 100
            Dim PostBufferLength As Short = 100
            If targetAddress < PreBufferLength Then PreBufferLength = targetAddress
            If rawtext.Length < targetAddress + PostBufferLength Then PostBufferLength = rawtext - targetAddress

            ' text before the found element
            Dim elementStringPre = rawtext.Substring(targetAddress - PreBufferLength, PreBufferLength)
            Dim elementSplitPre = elementStringPre.Split(" ").ToList

            elementSplitPre.Reverse()


            ' ========================
            ' get obstacle name
            ' ========================
            ' look until a new line is found
            Dim name_lst As New List(Of String)
            Dim name As String = ""
            For Each item In elementSplitPre
                If item <> "" Then
                    If item.Contains(vbLf) Then Exit For
                    name_lst.Add(item)
                End If
            Next

            ' invert the wording
            name_lst.Reverse()
            For Each item In name_lst
                name &= " " & item
            Next

            ' text after the found element
            Dim elementStringPost = rawtext.Substring(targetAddress, PostBufferLength)
            Dim elementSplitPost = elementStringPost.Split(" ").ToList



            ' ========================
            ' get obstacle type
            ' ========================
            Dim type_lst As New List(Of String)
            Dim type As String = ""
            Dim counter As Short = 0

            Dim matches As New List(Of String)
            For Each item In elementSplitPost
                matches.Add(item)
                ' find the first usefule word
                Dim p As String = "\b[A-Za-z]{4,}\b"

                ' Instantiate the regular expression object.
                Dim s As Regex = New Regex(p, RegexOptions.IgnoreCase)

                Dim q As Match = s.Match(item)

                ' use the second word found...
                If q.Success Then
                    Dim definition = checkWord(item)
                    If definition Then
                        counter += 1
                        ' A string is returned if the word exists in the dictionary
                        'Console.WriteLine(CStr(definition))

                        type_lst.Add(item)
                        If counter > 5 Then Exit For
                    End If
                End If
            Next



            For Each t In type_lst
                type &= " " & t
            Next

            ' ========================
            ' latitude
            ' ========================
            Dim patternLat As String = "[NS] [0-8][0-9] [0-5][0-9] [0-9][0-9]"

            ' Instantiate the regular expression object.
            Dim latex As Regex = New Regex(patternLat, RegexOptions.IgnoreCase)
            Dim latMatch As Match = latex.Match(elementStringPost)

            Dim latitudeString As String = latMatch.Value

            Dim latitude As Double = lat2double(latitudeString)

            ' ========================
            ' longitude
            ' ========================
            Dim patternLon As String = "[EW] [0-3][0-8][0-9] [0-5][0-9] [0-9][0-9]"



            ' Instantiate the regular expression object.
            Dim lonex As Regex = New Regex(patternLon, RegexOptions.IgnoreCase)
            Dim lonMatch As Match = lonex.Match(elementStringPost)

            Dim longitudeString As String = lonMatch.Value

            Dim longitude As Double = lon2double(longitudeString)


            Try
                ' parse Lat/Lon
                ' in DFS Germany always N E
                If latitudeString.Contains("N") = False Then
                    Console.Write("ERR: latitude not in northern Hemisphere! " & latitudeString)
                End If
                If longitudeString.Contains("E") = False Then
                    Console.Write("ERR: latitude not in eastern Hemisphere! " & longitudeString)
                End If
            Catch ex As Exception
            End Try

            If type = "" Then
                Console.ForegroundColor = ConsoleColor.Yellow
                Console.WriteLine("WARN: not found type! " & name)
                Console.ResetColor()
            End If

            ' ========================
            ' elevation  / height
            ' ========================
            Dim elevation As Double = 0
            Dim height As Double = 0
            Dim state As Short = 0
            Dim patternElev As String = "([1-9][0-9][0-9][0-9]|[1-9][0-9][0-9])"
            For Each item In elementSplitPost
                ' Instantiate the regular expression object.
                Dim elevex As Regex = New Regex(patternElev, RegexOptions.IgnoreCase)
                Dim elevMatch As Match = elevex.Match(item)

                If elevMatch.Success Then
                    If state = 0 Then
                        elevation = elevMatch.Value
                        state = 1
                    Else
                        height = elevMatch.Value
                        Exit For
                    End If
                End If
            Next



            If elevation = 0 Or height = 0 Then
                Console.ForegroundColor = ConsoleColor.Yellow
                Console.WriteLine("WARN: elevation or height not found! " & name)
                Console.ResetColor()
            End If


            ' ========================
            ' lighted  / marked
            ' ========================
            Dim lighted As Boolean = False
            Dim marked As Boolean = False
            Dim state2 As Short = 0
            For Each item In elementSplitPost
                If item.Contains("yes") Or item.Contains("no") Then
                    If state2 = 0 Then
                        If item.Contains("yes") Then
                            lighted = True
                        End If
                        state2 = 1
                    Else
                        If item.Contains("yes") Then
                            marked = True
                            Exit For
                        End If
                    End If
                End If
            Next


            ' cast type
            Dim nType As String = "TOWER"
            If type.Contains("TOWER") Then nType = "TOWER"
            If type.Contains("Wind") Then nType = "WINDTURBINE"
            If type.Contains("antenna") Then nType = "TOWER"
            If type.Contains("plant") Then nType = "CHIMNEY"
            If type.Contains("Crane") Then nType = "CRANE"
            If type.Contains("mast") Then nType = "MAST"


            Dim newObs As obstacleStruct
            newObs.name = name
            newObs.type = nType
            newObs.lat = latitude
            newObs.lon = longitude
            newObs.height = height & " FT"
            newObs.elevation = elevation
            newObs.marked = marked
            newObs.lighted = lighted
            parseResult.Add(newObs)

            Console.WriteLine((name.PadLeft(40)) & "-> " & nType.PadLeft(30) & " -> " & latitudeString & " | " & longitudeString & " -> elev: " & elevation.ToString.PadLeft(5) & " -> height: " & height.ToString.PadLeft(5) & " ->  lighted: " & lighted & " -> marked: " & marked)




            ' text after the found element

            m = m.NextMatch()

            '  Console.ReadKey()
        Loop



    End Sub

    Dim wordList As New List(Of String)
    Dim notFound As New List(Of String)
    Function checkWord(item As String)

        If notFound.Contains(item) Then Return False

        If wordList.Contains(item) Then
            Return True
        Else
            If CStr(Dictionary.GetDefinition(item)) <> "" Then
                wordList.Add(item)
                Return True
            Else
                notFound.Add(item)
                Return False
            End If
        End If
        Return False
    End Function

    Function lat2double(str As String) As Double
        Try

            Dim sp = str.Split(" ")
            Dim degr As Double = sp(1) + sp(2) / 60 + sp(3) / 3600
            Return degr
        Catch ex As Exception
            Console.WriteLine("ERR: cant recognize latitude! " & str)
        End Try
        Return Nothing
    End Function

    Function lon2double(str As String) As Double
        Try
            Dim sp = str.Split(" ")
            Dim degr As Double = sp(1) + sp(2) / 60 + sp(3) / 3600

            Return degr
        Catch ex As Exception
            Console.WriteLine("ERR: cant recognize longitude! " & str)
        End Try
        Return Nothing
    End Function



    Sub Main()


        Dim dir = getParameter("dir")

        writeHeader()


        Dim fileEntries As String() = Directory.GetFiles(dir)
        ' Process the list of files found in the directory.
        Dim fileName As String
        For Each fileName In fileEntries
            If fileName.Contains("5_4") And fileName.Contains(".pdf") Then

                Console.WriteLine("parsing document: " & fileName)
                Console.WriteLine("------------------------------------------------->")
                ParsePdfText(fileName)

                ' temp
                'Exit For
            End If
        Next

        createShapefile()
    End Sub


    Sub createShapefile()
        Dim fs As New FeatureSet(FeatureType.Line)
        fs.DataTable.Columns.Add(New DataColumn("id", Type.GetType("System.Int32")))

        fs.DataTable.Columns.Add(New DataColumn("type", Type.GetType("System.String")))
        fs.DataTable.Columns.Add(New DataColumn("name", Type.GetType("System.String")))
        fs.DataTable.Columns.Add(New DataColumn("height", Type.GetType("System.String")))
        fs.DataTable.Columns.Add(New DataColumn("elevation", Type.GetType("System.Int32")))

        Dim id = 0

        For Each cli In parseResult
            Dim cl As New Coordinate(cli.lon, cli.lat)


            Dim ffa As IFeature = fs.AddFeature(New Point(cl))
            ffa.DataRow.AcceptChanges()

            ffa.DataRow("id") = id
            ffa.DataRow("type") = cli.type
            ffa.DataRow("name") = cli.name
            ffa.DataRow("height") = cli.height
            ffa.DataRow("elevation") = cli.elevation

            id += 1
        Next
        fs.SaveAs("DFS_obstacles.shp", True)
    End Sub

    Dim sArgs_CLASS As String()
    Function getParameter(name As String)
        sArgs_CLASS = Environment.GetCommandLineArgs()
        For i As Short = 0 To sArgs_CLASS.Length - 2
            If "-" & name = sArgs_CLASS(i) Then Return sArgs_CLASS(i + 1)
        Next
        Return Nothing
    End Function
End Module
