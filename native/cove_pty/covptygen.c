#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <termios.h>
#include <unistd.h>

int main(int argc, char **argv) {
    if (argc != 2) {
        return 2;
    }
    long total = strtol(argv[1], NULL, 10);
    if (total < 0) {
        return 2;
    }

    struct termios t;
    if (tcgetattr(STDOUT_FILENO, &t) == 0) {
        cfmakeraw(&t);
        tcsetattr(STDOUT_FILENO, TCSANOW, &t);
    }

    unsigned char block[4096];
    for (int i = 0; i < 4096; i++) {
        block[i] = (unsigned char)(i % 256);
    }

    long written = 0;
    while (written < total) {
        long remaining = total - written;
        long chunk = remaining < 4096 ? remaining : 4096;
        long start = written % 256;
        long off = 0;
        while (off < chunk) {
            long n = chunk - off;
            long src = (start + off) % 256;
            long span = 256 - src;
            long copy = n < span ? n : span;
            ssize_t w = write(STDOUT_FILENO, block + src, (size_t)copy);
            if (w < 0) {
                return 1;
            }
            off += w;
        }
        written += chunk;
    }
    return 0;
}
