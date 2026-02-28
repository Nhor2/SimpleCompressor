# SimpleCompressor

![License](https://img.shields.io/badge/License-MIT-green.svg)
![VB.NET](https://img.shields.io/badge/VB.NET-blue.svg)
![WinForms](https://img.shields.io/badge/WinForms-.NET-lightblue.svg)
![.NET Framework](https://img.shields.io/badge/.NET_Framework-4.8.1-purple.svg)
![Platform](https://img.shields.io/badge/Platform-Windows-informational.svg)

E' un compressore di immagini bitmap non compresse.

'il compressore è:
'✅ Binary perfect
'✅ Streaming
'✅ Canonical Huffman
'✅ Header compatto (260 byte)
'✅ Compatibile .NET Framework 4.8.1


## 📖 Esempio d'uso

```vbnet

'Compressione
Dim compressor As New SimpleCompressor()
compressor.Compress(inputfile, outputfile)

'Decompressione
Dim data = compressor.Decompress(outputfile)
File.WriteAllBytes(roudtripfile, data)

```

## ⚖️ Licenza

Distribuito sotto licenza MIT. Vedi `LICENSE` per maggiori informazioni.

