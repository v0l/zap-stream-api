#!/bin/bash

ffmpeg 	\
	-f lavfi -i "sine=frequency=1000:sample_rate=48000" \
	-re -f lavfi -i testsrc -g 300 -r 60 -pix_fmt yuv420p -s 1280x720 \
	-c:v h264 -b:v 2000k -c:a aac -ac 2 -b:a 192k -f flv rtmp://localhost:9005/basic/$1

