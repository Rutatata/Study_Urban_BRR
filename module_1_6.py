# 1 Задача
my_dict = {'Rustam': 1998, 'Tatiana': 1997, 'Lena': 1996}
print(my_dict)
print(my_dict.get('Lena'))
print(my_dict.get('Sergey'))
my_dict.update({'Oleg': 2000, 'Andrey': 2001})
a = my_dict.pop('Rustam')
print(a)
print(my_dict)
# 2 Задача
my_set = {11, 11, 12, 13, 14, 15, 13}
print(my_set)
my_set.add(19)
my_set.add(25)
my_set.remove(12)
print(my_set)