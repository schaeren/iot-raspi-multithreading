dotnet publish --runtime linux-arm --no-self-contained  

scp -r .\bin\Debug\net6.0\linux-arm\publish\* pi@192.168.1.147:~/iot-raspi-multithreading
ssh pi@192.168.1.147 "chmod u+x ~/iot-raspi-multithreading/iot-raspi-multithreading"
