# Character Recognition using Template Matching

2021 December

**Platform**: C# (.Net 6), Azure

Using images from the MNIST dataset, the project can detect digits 0 ~ 9.

![Detects 2](screenshots/01.png)

However, the letter "X" is not in the dataset, so the model rejects "X" as invalid input.

![Cannot detect X](screenshots/02.png)

Add images of "X" to the dataset.

![Add data X](screenshots/03.png)

![View data X](screenshots/04.png)

Then retrain the model.

![Retrain model X](screenshots/05.png)

After retraining, the model can detect X.

![Detects X](screenshots/06.png)

[YouTube Video Presentation](https://www.youtube.com/watch?v=_n6jJSM8K3o)

Documentation: /docs/char_recognition.docx

NuGet Dependencies:
* Newtonsoft.Json
* Microsoft.Net.Sdk.Functions
* Azure.Storage.Blobs






