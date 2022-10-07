Для того чтобы использовать программу, необходимо:

1. Скачать модель "arcfaceresnet100-8.onnx" по ссылке: https://github.com/onnx/models/blob/main/vision/body_analysis/arcface/model/arcfaceresnet100-8.onnx. 

2. Поместить эту модель в папку "lib".

3. Перейти в папку "lib" и выполнить команду:
        ```
        dotnet pack
        ```

4. Перейти в папку "app" и выполнить команду:
        ```
        dotnet add package mr-nikulin-ArcFace
        ```

Для запуска программы:
1. Перейдите в app. Чтобы запустить обработку нескольких изображений (лежащих в папке images внутри папки app) ввести команду, например:
        ```
        dotnet run Obama1.png Obama2.png face1.png face2.png
        ```
2. Также если никакие аргументы командной строки не указаны, будут запущены некоторые тесты по умолчанию.
