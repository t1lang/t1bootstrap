#! /bin/sh

CSC=$(which mono-csc || which dmcs || which mcs || echo "none")

if [ $CSC = "none" ]; then
	echo "Error: please install mono-devel."
	exit 1
fi

set -e

$CSC /debug+ /out:t1boot.exe /main:T1Boot /res:src/kern.t1,t1-kernel src/*.cs
