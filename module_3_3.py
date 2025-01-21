def print_params(a = 1, b = 'строка', c = True):
    print(a, b, c)
print_params()
print_params(33, 'Огонь', False)
print_params(322, 'Воздушный')
print_params(b= 'Магия Металла')
print_params(b= 25)
print_params(c=[1, 2, 3])

values_list = [73, 'Строчка', False]
values_dict = {'a': 929, 'b': 'Сердце', 'c': True}
print_params(*values_list)
print_params(**values_dict)

values_list_2 = [22.31, 'Игра оконена']
print_params(*values_list_2, 42)