# Video2Brain (Lynda-Decryptor)

You may have noticed that if you want to download Videos from Video2Brain, the only way is to use the Video2Brain desktop app.
Now you can use this application to decrypt all videos and create a folder structure with normal titles instead of hash values.

## Usage
- Please download the latest binary from release section ([download](https://github.com/h4ck-rOOt/Lynda-Decryptor/releases/download/v1.3/LyndaDecryptor.zip))
- Extract the files with your favorit compression tool or buildin os functions.
- Open commandline and navigate to the extracted folder containing LyndaDecryptor.exe
- Execute LyndaDecryptor.exe with LyndaDecryptor /F [PathToEncryptedFile] [PathToDecryptedFile] or LyndaDecrytor /D [FolderWithEncryptedFiles] where you have to replace the "PathToEncryptedFile", "PathToDecryptedFile" or "FolderWithEncryptedFiles" with the relative or full path to your files.

There are some flags you can use to control the behavior:
- /RM will remove encrypted files after decryption
- /OUT is available to specify an output folder for decrypted files
- /DB [PATH] let you specify the full path to the database containing titles (to get rid of these odd names)

Converting subtitles is possible (thanks to @mdomnita) with this little tool [Repo](https://github.com/mdomnita/LyndaCaptionToSrtConvertor)

---

## Limitations
This program is **windows only** at the time of this writing. You can compile it under Linux and Mac using [Mono](http://www.mono-project.com/) or [DotNetCore](https://www.microsoft.com/net/core), with some changes due to the sqlite platform binding.

At the moment **decrypting files from android or ios apps are not supported**

---

![Lynda.com](https://upload.wikimedia.org/wikipedia/commons/thumb/5/5f/Video2brain.jpg/800px-Video2brain.jpg)
