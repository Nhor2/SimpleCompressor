Imports System.IO

Public Class Form1

    'il compressore è:
    '✅ Binary perfect
    '✅ Streaming
    '✅ Canonical Huffman
    '✅ Header compatto (260 byte)
    '✅ Compatibile .NET Framework 4.8.1

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Dim ofd As OpenFileDialog = New OpenFileDialog
        ofd.Filter = "Files bitmap BMP TIFF PNG JPG|*.bmp;*.tiff;*.jpg;*.png"
        ofd.FileName = ""
        ofd.Title = "Apri un immagine..."

        If ofd.ShowDialog(Me) <> DialogResult.OK Then Return

        Dim inputfile As String = ofd.FileName
        Dim outputfile As String = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(ofd.FileName), System.IO.Path.GetFileNameWithoutExtension(ofd.FileName) & ".huf")
        Dim roudtripfile As String = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(ofd.FileName), System.IO.Path.GetFileNameWithoutExtension(ofd.FileName) & "_HUFFA" & ".bmp")

        'Compressione
        Dim compressor As New SimpleCompressor()
        compressor.Compress(inputfile, outputfile)

        'Decompressione
        Dim data = compressor.Decompress(outputfile)
        File.WriteAllBytes(roudtripfile, data)

        MsgBox("File compresso e decompresso")
    End Sub
End Class


Public Class SimpleCompressor

    Public Sub Compress(inputPath As String, outputPath As String)

        Dim freqs(255) As Integer
        Dim originalLength As Integer = 0

        ' =============================
        ' PASS 1 – Calcolo frequenze (streaming)
        ' =============================
        Using fs As New FileStream(inputPath, FileMode.Open, FileAccess.Read)

            Dim prev As Integer = -1
            Dim b As Integer

            While True
                b = fs.ReadByte()
                If b = -1 Then Exit While

                originalLength += 1

                Dim currentOriginal As Integer = b
                Dim delta As Integer

                If prev = -1 Then
                    delta = currentOriginal
                Else
                    delta = (currentOriginal - prev) And &HFF
                End If

                freqs(delta) += 1
                prev = currentOriginal

            End While
        End Using

        ' =============================
        ' Costruzione Huffman
        ' =============================
        Dim root = BuildHuffmanTree(freqs)

        Dim codeLengths(255) As Integer
        BuildCodeLengths(root, 0, codeLengths)

        Dim canonicalCodes = BuildCanonicalCodes(codeLengths)

        ' =============================
        ' Scrittura file
        ' =============================
        Using fsOut As New FileStream(outputPath, FileMode.Create)
            Using bw As New BinaryWriter(fsOut)

                ' HEADER
                bw.Write(originalLength)

                For i = 0 To 255
                    bw.Write(CByte(codeLengths(i)))
                Next

                Dim bitWriter As New BitWriter(bw)

                ' PASS 2 – Scrittura bitstream
                Using fs As New FileStream(inputPath, FileMode.Open)

                    Dim prev As Integer = -1
                    Dim b As Integer

                    While True
                        b = fs.ReadByte()
                        If b = -1 Then Exit While

                        Dim currentOriginal As Integer = b
                        Dim delta As Integer

                        If prev = -1 Then
                            delta = currentOriginal
                        Else
                            delta = (currentOriginal - prev) And &HFF
                        End If

                        Dim code = canonicalCodes(CByte(delta))
                        bitWriter.WriteBits(code.Bits, code.Length)

                        prev = currentOriginal

                    End While

                End Using

                bitWriter.Flush()

            End Using
        End Using

    End Sub


    Public Function Decompress(inputPath As String) As Byte()

        Using fs As New FileStream(inputPath, FileMode.Open)
            Using br As New BinaryReader(fs)

                Dim originalLength = br.ReadInt32()

                Dim codeLengths(255) As Integer
                For i = 0 To 255
                    codeLengths(i) = br.ReadByte()
                Next

                Dim canonicalCodes = BuildCanonicalCodes(codeLengths)
                Dim decodeRoot = BuildTreeFromCanonical(canonicalCodes)

                Dim bitReader As New BitReader(br)

                Dim result(originalLength - 1) As Byte
                Dim index As Integer = 0
                Dim node = decodeRoot

                While index < originalLength

                    Dim bit = bitReader.ReadBit()

                    If bit = 0 Then
                        node = node.Left
                    Else
                        node = node.Right
                    End If

                    If node.IsLeaf Then
                        result(index) = CByte(node.Value)
                        index += 1
                        node = decodeRoot
                    End If

                End While

                ' Inverse Delta
                Dim prev As Integer = result(0)

                For i = 1 To result.Length - 1

                    Dim value As Integer = (result(i) + prev) And &HFF
                    result(i) = CByte(value)
                    prev = value

                Next

                Return result

            End Using
        End Using

    End Function

    ' =============================
    ' HUFFMAN
    ' =============================

    Private Function BuildHuffmanTree(freqs() As Integer) As HuffmanNode

        Dim heap As New MinHeap()

        For i = 0 To 255
            If freqs(i) > 0 Then
                heap.Insert(New HuffmanNode(i, freqs(i)))
            End If
        Next

        If heap.Count = 1 Then
            Dim singleNode = heap.ExtractMin()
            Return New HuffmanNode(-1, singleNode.Frequency, singleNode, Nothing)
        End If

        While heap.Count > 1

            Dim left = heap.ExtractMin()
            Dim right = heap.ExtractMin()

            Dim parent As New HuffmanNode(-1,
                                      left.Frequency + right.Frequency,
                                      left,
                                      right)

            heap.Insert(parent)

        End While

        Return heap.ExtractMin()

    End Function

    Private Function BuildCodes(root As HuffmanNode) As Dictionary(Of Byte, HuffCode)

        Dim dict As New Dictionary(Of Byte, HuffCode)
        BuildCodeRecursive(root, 0UI, 0, dict)
        Return dict

    End Function

    Private Function BuildCanonicalCodes(codeLengths() As Integer) _
    As Dictionary(Of Byte, HuffCode)

        Dim symbols = New List(Of Tuple(Of Byte, Integer))

        For i = 0 To 255
            If codeLengths(i) > 0 Then
                symbols.Add(Tuple.Create(CByte(i), codeLengths(i)))
            End If
        Next

        symbols = symbols.OrderBy(Function(s) s.Item2) _
                     .ThenBy(Function(s) s.Item1) _
                     .ToList()

        Dim dict As New Dictionary(Of Byte, HuffCode)

        Dim code As UInteger = 0
        Dim prevLen As Integer = 0

        For Each s In symbols

            code <<= (s.Item2 - prevLen)

            dict(s.Item1) = New HuffCode(code, s.Item2)

            code += 1
            prevLen = s.Item2

        Next

        Return dict

    End Function


    Private Sub BuildCodeLengths(node As HuffmanNode,
                             depth As Integer,
                             codeLengths() As Integer)

        If node Is Nothing Then Return

        If node.IsLeaf Then
            codeLengths(node.Value) = depth
            Return
        End If

        BuildCodeLengths(node.Left, depth + 1, codeLengths)
        BuildCodeLengths(node.Right, depth + 1, codeLengths)

    End Sub


    Private Function BuildTreeFromCanonical(
    codes As Dictionary(Of Byte, HuffCode)) As HuffmanNode

        Dim root As New HuffmanNode(-1, 0)

        For Each kv In codes

            Dim node = root
            Dim bits = kv.Value.Bits
            Dim length = kv.Value.Length

            For i = length - 1 To 0 Step -1

                Dim bit = (bits >> i) And 1UI

                If bit = 0 Then
                    If node.Left Is Nothing Then
                        node.Left = New HuffmanNode(-1, 0)
                    End If
                    node = node.Left
                Else
                    If node.Right Is Nothing Then
                        node.Right = New HuffmanNode(-1, 0)
                    End If
                    node = node.Right
                End If

            Next

            node.Value = kv.Key

        Next

        Return root

    End Function

    Private Sub BuildCodeRecursive(node As HuffmanNode,
                                   bits As UInteger,
                                   length As Integer,
                                   dict As Dictionary(Of Byte, HuffCode))

        If node Is Nothing Then Return

        If node.IsLeaf Then
            dict(CByte(node.Value)) = New HuffCode(bits, length)
            Return
        End If

        BuildCodeRecursive(node.Left, bits << 1, length + 1, dict)
        BuildCodeRecursive(node.Right, (bits << 1) Or 1UI, length + 1, dict)

    End Sub

End Class


' =============================
' HUFFMAN NODE
' =============================

Public Class HuffmanNode

    Public Property Value As Integer
    Public Property Frequency As Integer
    Public Property Left As HuffmanNode
    Public Property Right As HuffmanNode

    Public ReadOnly Property IsLeaf As Boolean
        Get
            Return Left Is Nothing AndAlso Right Is Nothing
        End Get
    End Property

    Public Sub New(val As Integer, freq As Integer,
                   Optional l As HuffmanNode = Nothing,
                   Optional r As HuffmanNode = Nothing)

        Value = val
        Frequency = freq
        Left = l
        Right = r

    End Sub

End Class


' =============================
' HUFF CODE STRUCT
' =============================

Public Structure HuffCode
    Public Bits As UInteger
    Public Length As Integer

    Public Sub New(b As UInteger, l As Integer)
        Bits = b
        Length = l
    End Sub
End Structure


' =============================
' BIT WRITER
' =============================

Public Class BitWriter

    Private bw As BinaryWriter
    Private buffer As UInteger = 0UI
    Private bitCount As Integer = 0

    Public Sub New(writer As BinaryWriter)
        bw = writer
    End Sub

    Public Sub WriteBits(bits As UInteger, length As Integer)

        For i As Integer = length - 1 To 0 Step -1

            ' Estrae un bit alla volta (dal più significativo)
            Dim bit As UInteger = (bits >> i) And 1UI

            buffer = (buffer << 1) Or bit
            bitCount += 1

            If bitCount = 8 Then
                bw.Write(CByte(buffer))
                buffer = 0UI
                bitCount = 0
            End If

        Next

    End Sub

    Public Sub Flush()

        If bitCount > 0 Then
            buffer <<= (8 - bitCount)
            bw.Write(CByte(buffer))
        End If

    End Sub

End Class


' =============================
' BIT READER
' =============================

Public Class BitReader

    Private br As BinaryReader
    Private buffer As Integer
    Private bitCount As Integer

    Public Sub New(reader As BinaryReader)
        br = reader
    End Sub

    Public Function ReadBit() As Integer

        If bitCount = 0 Then
            buffer = br.ReadByte()
            bitCount = 8
        End If

        bitCount -= 1
        Return (buffer >> bitCount) And 1

    End Function

End Class

Public Class MinHeap

    Private items As New List(Of HuffmanNode)

    Public ReadOnly Property Count As Integer
        Get
            Return items.Count
        End Get
    End Property

    Public Sub Insert(node As HuffmanNode)

        items.Add(node)
        HeapifyUp(items.Count - 1)

    End Sub

    Public Function ExtractMin() As HuffmanNode

        If items.Count = 0 Then
            Throw New InvalidOperationException("Heap vuoto")
        End If

        Dim root = items(0)
        Dim last = items(items.Count - 1)

        items(0) = last
        items.RemoveAt(items.Count - 1)

        If items.Count > 0 Then
            HeapifyDown(0)
        End If

        Return root

    End Function

    Private Sub HeapifyUp(index As Integer)

        While index > 0

            Dim parentIndex = (index - 1) \ 2

            If items(index).Frequency >= items(parentIndex).Frequency Then
                Exit While
            End If

            Swap(index, parentIndex)
            index = parentIndex

        End While

    End Sub

    Private Sub HeapifyDown(index As Integer)

        While True

            Dim left = index * 2 + 1
            Dim right = index * 2 + 2
            Dim smallest = index

            If left < items.Count AndAlso
               items(left).Frequency < items(smallest).Frequency Then
                smallest = left
            End If

            If right < items.Count AndAlso
               items(right).Frequency < items(smallest).Frequency Then
                smallest = right
            End If

            If smallest = index Then Exit While

            Swap(index, smallest)
            index = smallest

        End While

    End Sub

    Private Sub Swap(i As Integer, j As Integer)

        Dim tmp = items(i)
        items(i) = items(j)
        items(j) = tmp

    End Sub

End Class