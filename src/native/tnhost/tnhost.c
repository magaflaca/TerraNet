#include <dirent.h>
#include <dlfcn.h>
#include <limits.h>
#include <malloc.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#ifndef M_BIONIC_SET_HEAP_TAGGING_LEVEL
#define M_BIONIC_SET_HEAP_TAGGING_LEVEL (-204)
#endif

#ifndef M_HEAP_TAGGING_LEVEL_NONE
#define M_HEAP_TAGGING_LEVEL_NONE 0
#endif

typedef int (*hostfxr_main_startupinfo_fn)(int argc, const char **argv, const char *host_path, const char *dotnet_root, const char *app_path);

static int find_hostfxr(const char *root, char *out, size_t size)
{
    char fxr_dir[PATH_MAX];
    snprintf(fxr_dir, sizeof fxr_dir, "%s/host/fxr", root);
    DIR *dir = opendir(fxr_dir);
    if (!dir)
        return -1;
    struct dirent *entry;
    int found = -1;
    while ((entry = readdir(dir)) != NULL)
    {
        if (entry->d_name[0] == '.')
            continue;
        snprintf(out, size, "%s/%s/libhostfxr.so", fxr_dir, entry->d_name);
        found = 0;
        break;
    }
    closedir(dir);
    return found;
}

static void strip_extension(const char *path, char *out, size_t size)
{
    snprintf(out, size, "%s", path);
    char *dot = strrchr(out, '.');
    char *slash = strrchr(out, '/');
    if (dot && (!slash || dot > slash))
        *dot = '\0';
}

int main(int argc, char **argv)
{
    mallopt(M_BIONIC_SET_HEAP_TAGGING_LEVEL, M_HEAP_TAGGING_LEVEL_NONE);

    if (argc < 3)
    {
        fprintf(stderr, "usage: %s <dotnet-root> <app.dll> [args...]\n", argv[0]);
        return 2;
    }

    char root[PATH_MAX];
    if (!realpath(argv[1], root))
    {
        fprintf(stderr, "dotnet root not found: %s\n", argv[1]);
        return 3;
    }

    char app_path[PATH_MAX];
    if (!realpath(argv[2], app_path))
    {
        fprintf(stderr, "app not found: %s\n", argv[2]);
        return 3;
    }

    char fxr_path[PATH_MAX];
    if (find_hostfxr(root, fxr_path, sizeof fxr_path) != 0)
    {
        fprintf(stderr, "hostfxr not found under %s\n", root);
        return 4;
    }

    void *fxr = dlopen(fxr_path, RTLD_NOW | RTLD_LOCAL);
    if (!fxr)
    {
        fprintf(stderr, "dlopen failed: %s\n", dlerror());
        return 5;
    }

    hostfxr_main_startupinfo_fn start = (hostfxr_main_startupinfo_fn)dlsym(fxr, "hostfxr_main_startupinfo");
    if (!start)
    {
        fprintf(stderr, "hostfxr_main_startupinfo missing\n");
        return 6;
    }

    char host_path[PATH_MAX];
    strip_extension(app_path, host_path, sizeof host_path);

    int host_argc = argc - 2;
    const char **host_argv = calloc((size_t)host_argc + 1, sizeof(char *));
    host_argv[0] = host_path;
    for (int i = 3; i < argc; i++)
        host_argv[i - 2] = argv[i];

    setvbuf(stdout, NULL, _IOLBF, 0);
    return start(host_argc, host_argv, host_path, root, app_path);
}
